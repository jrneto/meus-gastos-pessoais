# Plano técnico — FEAT-09: Esteira de CI/CD (GitHub Actions)

Consulte `spec.md` desta feature para requisitos e critérios de aceite.
Este plano assume aprovação prévia do usuário para qualquer `apply`
real na AWS (nenhum comando desse tipo roda de forma autônoma).

## Camadas afetadas

| Camada | O que muda |
|---|---|
| `frontend/app/src/` | Novo componente compartilhado (`AppVersion`) + util (`getAppVersion`) que lê variáveis de build-time e monta o link para a release/commit no GitHub. Consumido pela `SettingsPage`. Extensão de tipos do `import.meta.env`. |
| `frontend/app/` (config) | `vite.config.ts` sem mudança estrutural (Vite já expõe `process.env.VITE_*` para `import.meta.env` automaticamente — não precisa de `define` manual). Nenhum novo `.env.*.example` necessário: as novas variáveis (`VITE_APP_VERSION`, `VITE_APP_COMMIT_SHA`) são só setadas em runtime de CI, nunca localmente. |
| `.github/workflows/` (raiz do monorepo) | Dois workflows novos: `frontend-deploy-hom.yml` e `frontend-deploy-prod.yml`. Nenhum workflow de backend é tocado (fora de escopo). |
| `frontend/infra/terraform/` | Nova configuração `cicd/` (camada persistente, mesmo padrão de `dns/`): OIDC Provider do GitHub + IAM Role assumida pelos workflows, com permissão restrita aos buckets/distribuições de hom e prod já existentes. Nenhuma mudança em `environments/{prod,hom}/` ou `dns/`. |
| `frontend/infra/CLAUDE.md` | Atualizado ao final para documentar a esteira de CI/CD e a nova config `cicd/`. |

## Estratégia de branch/release (decisão de gatilho)

O repositório já segue um fluxo com duas branches de longa duração
(`develop` e `main`, confirmado em `git branch -a`) — a esteira reaproveita
isso em vez de introduzir um modelo novo:

- **Push em `develop`** (inclui merge de PR) → dispara
  `frontend-deploy-hom.yml` → build aponta para
  `https://api-hom.jrnexpenses.com` → publica em `hom.jrnexpenses.com`.
- **GitHub Release publicada** (tag semântica `vX.Y.Z`, tipicamente
  criada a partir de `main`) → dispara `frontend-deploy-prod.yml` →
  build faz checkout exatamente na tag da release → aponta para
  `https://api.jrnexpenses.com` → publica em `jrnexpenses.com`.

A criação da release em si (versionamento semântico, changelog) **não
é automatizada por esta feature** (ver "Fora do escopo" em `spec.md`) —
o usuário cria a release manualmente pelo GitHub quando decide
promover `main` para produção.

## Contratos técnicos

### Variáveis de build injetadas pelo CI (não versionadas)

| Variável | Setada por | Valor em hom | Valor em prod |
|---|---|---|---|
| `VITE_API_BASE_URL` | Workflow (`env:` do step de build) | `https://api-hom.jrnexpenses.com` | `https://api.jrnexpenses.com` |
| `VITE_APP_VERSION` | Workflow | `dev-<short-sha>` (ex.: `dev-a1b2c3d`) | `${{ github.event.release.tag_name }}` (ex.: `v1.4.0`) |
| `VITE_APP_COMMIT_SHA` | Workflow | `${{ github.sha }}` (7 chars) | SHA do commit referenciado pela tag da release |

Vite já mescla `process.env` com prefixo `VITE_` em `import.meta.env`
em tempo de build (`loadEnv`, comportamento nativo) — não é necessário
`define` customizado em `vite.config.ts` nem arquivo `.env` físico em
CI.

### `src/lib/appVersion.ts` (novo, compartilhado)

```ts
export interface AppVersionInfo {
  version: string          // import.meta.env.VITE_APP_VERSION (fallback 'dev-local')
  commitSha: string        // import.meta.env.VITE_APP_COMMIT_SHA (fallback 'local')
  isRelease: boolean       // true se version bate com padrão semver vX.Y.Z
  url: string              // link p/ release (prod) ou commit (hom/local)
}

export function getAppVersion(): AppVersionInfo
```

- `isRelease` = `/^v\d+\.\d+\.\d+$/.test(version)`.
- Se `isRelease`: `url = https://github.com/jrneto/meus-gastos-pessoais/releases/tag/${version}`.
- Senão: `url = https://github.com/jrneto/meus-gastos-pessoais/commit/${commitSha}`.
- Fallback local (sem as env vars, ex.: `npm run dev`): `version = 'dev-local'`, `commitSha = 'local'`, sem link quebrado — componente pode omitir o link nesse caso.

### `src/components/AppVersion.tsx` (novo, compartilhado — `components/`, não `features/`, por ser usado potencialmente em mais de um lugar)

Componente simples: texto da versão + link (`<a target="_blank" rel="noreferrer">`) para `url` de `getAppVersion()`. Renderizado na `SettingsPage` (`frontend/app/src/routes/SettingsPage.tsx`), abaixo do botão de logout — sem necessidade de nova rota.

### `vite-env.d.ts` (extensão de tipos)

```ts
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_APP_VERSION?: string
  readonly VITE_APP_COMMIT_SHA?: string
}
```

### Workflows GitHub Actions

**`.github/workflows/frontend-deploy-hom.yml`**
- `on: push: branches: [develop], paths: ['frontend/app/**']` — restringe o gatilho a mudanças reais no app, evitando consumo de minutos por mudanças só em `backend/` ou `docs/` (relevante para a cota de 2.000 min/mês).
- `permissions: id-token: write, contents: read` (necessário para OIDC).
- `environment: hom` (GitHub Environment — escopo de variáveis, não exige plano pago; sem "required reviewers", que é recurso pago em repo privado).
- Jobs: `quality` (checkout → `actions/setup-node` com cache npm → `npm ci` → `npm run lint` → `npm run test`) → `deploy` (precisa de `quality`; `npm run build` com as env vars de hom → `configure-aws-credentials` via OIDC assumindo a Role → `aws s3 sync dist/ s3://gastosapp-frontend-hom/ --delete` → `aws cloudfront create-invalidation --distribution-id <hom> --paths "/*"`).

**`.github/workflows/frontend-deploy-prod.yml`**
- `on: release: types: [published]`.
- Mesma estrutura de jobs (`quality` → `deploy`), fazendo checkout na tag da release (`ref: ${{ github.event.release.tag_name }}` — comportamento padrão do evento `release`).
- `environment: prod`.
- Publica em `gastosapp-frontend-prod` + invalida a distribuição de prod.
- **Sem filtro de `paths`** (diferente de hom): uma release pode ser cortada de qualquer estado de `main`, e queremos garantir que o conteúdo publicado sempre corresponda exatamente à tag, independente de quais arquivos mudaram desde a última release.

IDs de distribuição CloudFront (hom `ELE195A1APCLB`, prod — obter via
`terraform output` em `environments/prod/`) e nomes de bucket ficam
como `vars` do GitHub Environment (não são segredo), para os workflows
lerem sem hardcode duplicado.

## Recursos AWS usados/afetados

**Recurso novo (requer aprovação explícita antes do `apply`):**
- `aws_iam_openid_connect_provider` para `https://token.actions.githubusercontent.com` — **verificar antes via `aws iam list-open-id-connect-providers` se já existe na conta** (é um recurso único por conta/URL; se algum processo anterior já o criou, a config deste plano deve importá-lo em vez de criar um novo, para não colidir).
- `aws_iam_role` (ex.: `gastosapp-frontend-cicd`), com:
  - **Trust policy** condicionada ao `sub` do token OIDC, restrita ao
    repositório (`repo:jrneto/meus-gastos-pessoais:ref:refs/heads/develop`
    e `repo:jrneto/meus-gastos-pessoais:ref:refs/tags/v*` — nunca
    `repo:jrneto/meus-gastos-pessoais:*` genérico, para não permitir que
    qualquer branch/PR assuma a role).
  - **Policy de permissão**, escopada só ao necessário:
    - `s3:PutObject`, `s3:DeleteObject`, `s3:ListBucket` restritos aos
      ARNs de `gastosapp-frontend-hom` e `gastosapp-frontend-prod`
      (nenhum outro bucket da conta).
    - `cloudfront:CreateInvalidation` restrito aos ARNs das
      distribuições de hom e prod (nenhuma outra distribuição da conta).
  - Sem custo (IAM é gratuito).

**Recursos existentes usados, não alterados:**
- Buckets S3 `gastosapp-frontend-hom`/`gastosapp-frontend-prod` e
  distribuições CloudFront correspondentes (FEAT-07/FEAT-08) — a
  esteira só escreve conteúdo neles, não muda configuração.

**Nenhum recurso com custo fixo é introduzido.** `CreateInvalidation`
tem 1.000 invalidações de paths grátis/mês por distribuição — o volume
de deploys deste projeto fica muito abaixo disso.

## Decisões técnicas e trade-offs

- **Por que `cicd/` como config Terraform separada (e não dentro de
  `environments/{prod,hom}/`)**: a Role/OIDC Provider não pertence ao
  ciclo de vida de nenhum ambiente específico — é usada por ambos e não
  deve ser destruída se um ambiente for recriado. Mesmo racional já
  aplicado a `dns/` (camada persistente e compartilhada).
- **Por que gate de qualidade roda antes do build de deploy, não em
  paralelo**: falha rápida (`quality` antes de `deploy`) evita gastar
  minutos de Actions rodando build+upload quando o teste já falhou —
  relevante para a cota gratuita.
- **Por que não usar `AWS_ACCESS_KEY_ID`/secret fixo**: exigência
  explícita da spec (US7) — OIDC elimina credencial de longa duração
  armazenada no GitHub, reduzindo superfície de vazamento.
- **Por que a versão de homologação não usa tag semântica**: nem todo
  push em `develop` corresponde a uma release — usar o SHA curto evita
  sugerir uma versão "oficial" que não existe como release, mantendo a
  US4 (rastreabilidade sem ambiguidade com produção).
- **Cache de dependências no workflow** (`actions/setup-node` com
  `cache: npm`) — reduz tempo de execução e, por consequência, minutos
  consumidos da cota gratuita.
- **`npm ci` (não `npm install`)** em CI — determinístico, respeita o
  lockfile, prática padrão para pipelines.

## Tratamento de falhas do pipeline

Não há mapeamento de `Error`/`ErrorType`/HTTP aqui (não é uma API) — o
equivalente é o resultado do workflow:

| Etapa que falha | Efeito |
|---|---|
| `npm run lint` | Job `quality` falha → `deploy` nunca roda → nada é publicado |
| `npm run test` | Idem |
| `npm run build` | Job `deploy` falha antes do `s3 sync` → nada é publicado |
| `aws s3 sync` | Job falha; possível estado parcial no bucket (mesmo risco do processo manual atual — não é regressão) |
| `aws cloudfront create-invalidation` | Job falha após o `sync` já ter publicado; conteúdo novo já está no bucket mas pode não estar imediatamente visível via cache antigo até nova invalidação (manual ou próximo deploy) |

Sem rollback automático (fora de escopo, conforme `spec.md`).

## Pontos que precisam de confirmação do usuário antes do `/tasks`

1. **Nome/local exato do componente de versão**: proposto `SettingsPage`
   (rodapé da tela "Configurações"). Confirmar se é o lugar desejado ou
   se prefere um rodapé global visível em todas as telas.
2. **Confirmar que o gatilho de hom deve ser push direto em `develop`**
   (não PR aberto/sincronizado) — evita deploy de branches de feature
   ainda não integradas.
3. **Aprovação explícita para o `terraform apply`** que cria o OIDC
   Provider + IAM Role em `frontend/infra/terraform/cicd/` — nenhuma
   execução real acontece sem esse sinal, inclusive a verificação de
   OIDC Provider pré-existente.
4. **Nome da IAM Role e dos GitHub Environments** (`hom`/`prod`)
   propostos acima — confirmar ou ajustar nomenclatura.
5. Confirmar que **não há necessidade de aprovação manual (required
   reviewer) antes do deploy de produção** além da própria criação da
   release — dado que esse recurso de proteção de Environment é pago
   para repositórios privados.
