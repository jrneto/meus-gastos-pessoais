# Plano técnico — FEAT-10: Fluxo de branch por feature + PRs automáticos

Consulte `spec.md` desta feature para requisitos e critérios de
aceite. Este plano assume aprovação prévia do usuário para a mudança
de configuração do repositório (habilitar Actions a abrir PR) — nenhum
comando desse tipo roda de forma autônoma.

## Camadas afetadas

Não há camadas de aplicação (Api/Application/Domain/Infra) envolvidas
— é uma feature 100% de CI/CD, sem código de app.

| Arquivo | Mudança |
|---|---|
| `.github/workflows/frontend-feature-pr.yml` | **Novo**. Roda o gate de qualidade em qualquer branch `FEAT-*` que altere `frontend/app/**`; se passar, abre PR pra `develop` (idempotente). |
| `.github/workflows/frontend-deploy-prod.yml` | **Modificado**. Novo job `open-pr-main`, após `deploy`, abre PR `develop → main` (idempotente). |
| `CLAUDE.md` (raiz) | Já atualizado no `/specify` — sem mudança adicional aqui. |
| `frontend/app/src/**` | Sem mudança — nenhum código de aplicação nesta feature. |

## Estratégia de gatilho e idempotência

**`frontend-feature-pr.yml`**:
- `on: push: branches: ["FEAT-*"], paths: ["frontend/app/**"]` — mesmo
  princípio de economia de minutos já usado em `frontend-deploy-hom.yml`
  (FEAT-09): só roda quando há código de fato pra testar.
- Job `quality`: idêntico ao já usado em `frontend-deploy-hom.yml`/
  `frontend-deploy-prod.yml` (checkout, `setup-node`, `npm ci`,
  `npm run lint`, `npm run test`) — sem duplicar lógica nova, só
  replicar o step existente (não há suporte nativo do GitHub Actions
  pra "importar" jobs entre arquivos sem Reusable Workflows, que
  adicionariam complexidade desproporcional pro tamanho do projeto).
- Job `open-pr` (`needs: quality`): usa a `gh` CLI (pré-instalada nos
  runners `ubuntu-latest`, autenticada via `GITHUB_TOKEN` automático —
  sem OIDC, sem secret novo) para checar se já existe PR aberto
  `<branch> → develop`; só cria um novo se não existir.

**`frontend-deploy-prod.yml`** (job novo `open-pr-main`, `needs: deploy`):
- Mesma lógica de idempotência, checando PR aberto `develop → main`.
- Roda só depois que `deploy` (build + s3 sync + invalidação) já
  terminou com sucesso — se o deploy falhar, o job nem começa
  (`needs`), então nenhum PR é aberto para uma release que não chegou
  a ir pro ar.

## Contratos técnicos

### Job `open-pr` (`frontend-feature-pr.yml`)

```yaml
open-pr:
  needs: quality
  runs-on: ubuntu-latest
  permissions:
    pull-requests: write   # só este job precisa — quality fica só com contents: read
    contents: read
  steps:
    - uses: actions/checkout@v4
    - name: Abrir PR para develop (se ainda não existir)
      env:
        GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        existing=$(gh pr list --head "${{ github.ref_name }}" --base develop \
          --state open --json number --jq 'length')
        if [ "$existing" -eq 0 ]; then
          gh pr create --base develop --head "${{ github.ref_name }}" \
            --title "${{ github.ref_name }}" \
            --body "Aberto automaticamente pelo gate de qualidade (frontend-feature-pr.yml) — FEAT-10."
        else
          echo "PR já existe para ${{ github.ref_name }} → develop, nada a fazer."
        fi
```

- `github.ref_name` = nome da branch que dparou o push (ex.:
  `FEAT-11-nome-feature`) — usado tanto como `--head` quanto como
  título do PR (mesma convenção: branch nomeada como a spec, título do
  PR igual à branch).
- `gh pr list --json number --jq 'length'` é a forma idempotente de
  checar existência sem depender de parsing frágil de texto.

### Job `open-pr-main` (`frontend-deploy-prod.yml`)

```yaml
open-pr-main:
  needs: deploy
  runs-on: ubuntu-latest
  permissions:
    pull-requests: write
    contents: read
  steps:
    - uses: actions/checkout@v4
    - name: Abrir PR develop → main (se ainda não existir)
      env:
        GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        existing=$(gh pr list --head develop --base main \
          --state open --json number --jq 'length')
        if [ "$existing" -eq 0 ]; then
          gh pr create --base main --head develop \
            --title "Release ${{ github.event.release.tag_name }} → main" \
            --body "Aberto automaticamente após deploy de produção bem-sucedido (release \`${{ github.event.release.tag_name }}\`)."
        else
          echo "PR develop → main já existe, nada a fazer."
        fi
```

- Título inclui a tag da release que disparou o deploy — dá
  rastreabilidade de qual release motivou a atualização de `main`.

## Decisões técnicas e trade-offs

- **`gh` CLI em vez de uma Action de terceiros** (ex.:
  `peter-evans/create-pull-request`): já vem instalada nos runners
  hospedados pelo GitHub, autentica sozinha via `GITHUB_TOKEN`, e a
  lógica de idempotência (checar antes de criar) é simples o
  suficiente pra não precisar de dependência externa — menos
  superfície, menos coisa pra manter atualizada.
- **Checagem de idempotência via `gh pr list` antes de `gh pr create`**,
  em vez de deixar o `gh pr create` falhar e ignorar o erro
  (`continue-on-error`): mais explícito nos logs (mostra claramente
  "PR já existe, nada a fazer" em vez de um step "falho" mascarado) e
  não esconde falhas reais de permissão atrás de um `continue-on-error`
  genérico.
- **Permissão `pull-requests: write` escopada só nos 2 jobs novos**
  (`open-pr`, `open-pr-main`), não no workflow inteiro — os jobs
  `quality`/`deploy` continuam com o mínimo necessário (princípio de
  menor privilégio, mesma lógica já aplicada à Role IAM da FEAT-09).
- **Sem Reusable Workflow pra não duplicar o job `quality`**: o
  projeto já duplica esse job 2x (hom/prod, FEAT-09); duplicar mais
  uma vez é aceitável no tamanho atual do projeto — Reusable Workflows
  adicionariam indireção (arquivo `workflow_call` separado) sem
  benefício claro ainda. Se o número de workflows crescer bastante no
  futuro, vale reconsiderar.
- **`frontend-feature-pr.yml` não sobrepõe `frontend-deploy-hom.yml`**:
  o segundo só dispara em push em `develop`; o novo só dispara em push
  em branches `FEAT-*`. Sem colisão de gatilho — uma branch nunca é
  `develop` e `FEAT-*` ao mesmo tempo.

## Recursos AWS

**Nenhum recurso AWS novo ou afetado.** Esta feature é só GitHub
Actions + API do GitHub — não toca S3, CloudFront, IAM ou qualquer
outro serviço AWS.

## Configuração de repositório necessária (fora do Terraform/AWS)

Antes de qualquer um dos dois workflows funcionar, é preciso habilitar
no GitHub: **Settings → Actions → General → Workflow permissions →
"Allow GitHub Actions to create and approve pull requests"** (hoje
desabilitado por padrão). Sem isso, `gh pr create` falha com
permissão negada mesmo com `permissions: pull-requests: write` no
YAML — são dois controles independentes (permissão do workflow +
permissão do repositório). **Exige aprovação explícita do usuário
antes de ser habilitado** (US7 da spec).

## Tratamento de falhas

| Etapa que falha | Efeito |
|---|---|
| `npm run lint`/`npm run test` (job `quality`, `frontend-feature-pr.yml`) | Job `open-pr` nunca roda (`needs: quality`) — nenhum PR é aberto para código que não passou no gate |
| `gh pr create` (permissão do repo não habilitada) | Job falha, mas não afeta `develop`/`main` — só significa que o PR precisa ser aberto manualmente dessa vez |
| `deploy` (`frontend-deploy-prod.yml`) falha | Job `open-pr-main` nunca roda — nenhum PR pra `main` é aberto para uma release que não foi ao ar com sucesso |

Sem rollback ou notificação adicional — mesmo padrão já estabelecido
na FEAT-09.

## Pontos que precisam de confirmação do usuário antes do `/tasks`

1. **Aprovação explícita para habilitar** "Allow GitHub Actions to
   create and approve pull requests" nas configurações do repositório
   — sem isso, nenhum dos dois workflows funciona de verdade (posso só
   confirmar isso na hora da execução, não precisa decidir agora).
2. **Título/corpo dos PRs automáticos** propostos acima — confirmar se
   o texto serve ou se prefere algo diferente (ex.: incluir um resumo
   do `spec.md` no corpo do PR branch→develop, em vez de um texto
   fixo).
3. Confirmar que branch da feature = título do PR é aceitável (sem
   nenhuma formatação adicional, ex.: sem remover o prefixo `FEAT-XX-`).
