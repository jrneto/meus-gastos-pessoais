# FEAT-08: Ambiente de homologação do frontend

## Objetivo

Criar um ambiente de **homologação** para o frontend, isolado do
ambiente de produção existente, duplicando as peças de hosting já
provisionadas (bucket S3 + distribuição CloudFront + certificado ACM),
de forma que seja possível validar builds do frontend contra a API de
homologação do backend (`https://api-hom.jrnexpenses.com`, já existente
desde `backend/specs/FEAT-13-ambiente-homologacao/`) antes de ir para
produção, sem risco de afetar o site real. O frontend de homologação
deve responder em `https://hom.jrnexpenses.com`, mantendo o custo o
mais próximo possível de zero.

## Contexto

Até aqui (FEAT-07), toda a infraestrutura provisionada em
`frontend/infra/terraform/` — bucket S3 (`gastosapp-frontend-prod`),
distribuição CloudFront, certificado ACM (`jrnexpenses.com`/
`www.jrnexpenses.com`), WAF WebACL e os records DNS do frontend na
hosted zone `jrnexpenses.com.` — corresponde exclusivamente ao ambiente
de **produção**. Não existe hoje nenhum ambiente separado para validar
mudanças de frontend antes de expô-las publicamente.

Esta feature duplica hosting + domínio para um novo ambiente de
homologação, mantendo produção intacta e sem alteração de comportamento.
A própria FEAT-07 já deixou o terreno preparado para isso: a config
principal já nasceu em `environments/prod/` (em vez de
`frontend/infra/terraform/` direto) justamente para que homologação vire
`environments/hom/` sem mover state de uma estrutura plana; nomes hoje
fixos (domínio, nome do bucket) já ficam em `variables.tf`. A hosted
zone `jrnexpenses.com.` (camada persistente `dns/`, FEAT-07) já existe e
é reaproveitada — o novo record `hom.jrnexpenses.com` é criado dentro
dela, sem duplicar ou recriar a zona.

Precedente direto de estilo/abordagem:
`backend/specs/FEAT-13-ambiente-homologacao/` (mesmo conceito aplicado
ao backend — DynamoDB/Cognito/Parameter Store/Lambda/API Gateway
duplicados para homologação, API respondendo em
`api-hom.jrnexpenses.com`). A diferença central é que ali os recursos já
existiam e foram importados (FEAT-09/10/12); aqui os recursos de
homologação são **novos** (criados do zero via Terraform, seguindo o
mesmo padrão de "duas camadas" já usado em produção pela FEAT-07: DNS
persistente + config de ambiente efêmera).

## Requisitos de negócio / restrições

- **Isolamento total de produção**: o ambiente de homologação deve ter
  seu próprio bucket S3, sua própria distribuição CloudFront e seu
  próprio certificado ACM. Nenhuma operação de deploy em homologação
  pode afetar o conteúdo, a configuração ou a disponibilidade dos
  recursos de produção, e vice-versa.
- **Aponta para a API de homologação**: o build publicado em
  `hom.jrnexpenses.com` deve ser gerado com `VITE_API_BASE_URL` apontando
  para `https://api-hom.jrnexpenses.com` (já existente,
  `backend/specs/FEAT-13-ambiente-homologacao/`), nunca para a API de
  produção.
- **Rota pública**: o frontend de homologação deve responder em
  `https://hom.jrnexpenses.com`, seguindo o mesmo padrão de domínio
  customizado + certificado ACM + record DNS já usado em produção
  (FEAT-07), dentro da hosted zone `jrnexpenses.com.` já existente
  (camada persistente `dns/`) — sem duplicar ou recriar essa zona.
- **Custo baixo (idealmente zero/free tier)**: mesmas restrições de
  custo já vigentes em `frontend/docs/constitution.md` e no `/CLAUDE.md`
  raiz aplicam-se a homologação:
  - Bucket S3 sem custo fixo (cobrança por armazenamento/requisição,
    volume desprezível em homologação).
  - Certificado ACM público: gratuito.
  - Record DNS adicional na zona já existente: sem custo incremental
    (zona já é cobrada por produção).
  - **CloudFront/WAF exigem atenção**: hoje a conta está no plano
    **CloudFront Free** (WAF + proteção DDoS + 100GB de transferência
    sem cobrança adicional), que cobre **1 distribuição** (ver
    `frontend/specs/FEAT-07-terraform-import-infra/spec.md`). Uma
    segunda distribuição CloudFront para homologação pode tirar a conta
    desse limite e gerar cobrança de WAF (~US$5 base + ~US$1/regra/mês)
    e/ou do próprio CloudFront Free. Esta spec não decide sozinha como
    tratar isso (ex.: hom sem WAF, hom compartilhando o mesmo padrão de
    prod aceitando o risco de custo, ou outra estratégia) — a decisão
    técnica fica para o `plan.md`, mas **exige validação explícita do
    usuário antes de qualquer `apply`**, dado o histórico de "nível de
    preocupação alto" para custo AWS.
- **Nenhuma ação na conta AWS sem autorização prévia explícita do
  usuário** — vale tanto para o desenho da estratégia (`plan.md`) quanto
  para qualquer execução futura (`terraform plan`/`terraform apply`).
  Nenhum comando que possa criar, alterar ou destruir recursos reais
  roda de forma autônoma.
- **Nenhuma mudança de comportamento observável em produção**: o site em
  `https://jrnexpenses.com`/`https://www.jrnexpenses.com` continua
  respondendo exatamente como hoje, sem nenhuma alteração de
  configuração, conteúdo ou disponibilidade.
- **IaC exclusivamente em Terraform**, seguindo a mesma convenção já
  usada em `frontend/infra/terraform/` (ver `frontend/infra/CLAUDE.md`)
  — a organização exata (nova pasta `environments/hom/` ao lado de
  `environments/prod/`, reaproveitando a mesma camada `dns/`) é uma
  decisão técnica do `plan.md`, mas segue o padrão de duas camadas já
  estabelecido pela FEAT-07.
- **CORS no backend fica fora desta feature**: para o frontend de
  homologação chamar `https://api-hom.jrnexpenses.com` do navegador, a
  API de homologação precisa liberar essa origem em CORS. Essa mudança
  pertence ao contexto **backend** (mesmo princípio já registrado em
  `frontend/docs/constitution.md` — CORS é resolvido "de dentro" do
  contexto backend, não do frontend) e não faz parte desta spec.
- **Deploy do build em si continua manual**: esta feature provisiona a
  infraestrutura (bucket, distribuição, certificado, record DNS); o
  processo de build (`npm run build`) + upload do conteúdo para o bucket
  de homologação não é automatizado por esta spec.

## User Stories

**US1 — Hosting isolado em homologação**
- Given o ambiente de homologação provisionado
- When um build do frontend é publicado no bucket S3 de homologação
- Then ele é servido por uma distribuição CloudFront própria de
  homologação, e o bucket/distribuição de produção permanecem
  inalterados

**US2 — Frontend de homologação acessível pelo domínio customizado**
- Given o ambiente de homologação provisionado, com certificado ACM
  válido para `hom.jrnexpenses.com`
- When uma requisição é feita para `https://hom.jrnexpenses.com`
- Then ela é respondida pela distribuição CloudFront de homologação,
  servindo o conteúdo do bucket de homologação via HTTPS

**US3 — Frontend de homologação consome a API de homologação**
- Given o frontend de homologação publicado com
  `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com`
- When uma ação que chama a API é feita a partir de
  `https://hom.jrnexpenses.com` (ex.: login, listagem de despesas)
- Then a chamada é feita contra `https://api-hom.jrnexpenses.com`, nunca
  contra a API de produção

**US4 — Produção sem regressão**
- Given o ambiente de homologação provisionado
- When requisições continuam sendo feitas para
  `https://jrnexpenses.com`/`https://www.jrnexpenses.com` (produção)
- Then o comportamento observado é idêntico ao anterior à esta feature —
  mesmo conteúdo, mesma disponibilidade

**US5 — Custo controlado e decisão de WAF explícita**
- Given a estratégia de homologação definida em `plan.md` (com ou sem
  WAF na distribuição de homologação)
- When os recursos de homologação são provisionados
- Then nenhum recurso gera custo fixo por hora ligada, e qualquer
  cobrança potencial identificada (ex.: WAF de uma segunda distribuição
  saindo do plano CloudFront Free) foi explicitamente apresentada e
  aprovada pelo usuário antes do `apply`

**US6 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que possa criar, alterar ou destruir um recurso
  AWS (`terraform plan`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nenhum comando desse tipo roda de forma autônoma

## Contratos observáveis

Não há contrato de API novo — o frontend de homologação serve o mesmo
SPA já existente, sem nenhuma mudança de comportamento, rota ou tela. A
única diferença observável é: (1) a **base URL de hosting**, que passa a
responder também em `https://hom.jrnexpenses.com`, além de
`https://jrnexpenses.com`/`https://www.jrnexpenses.com` (produção,
inalterado); e (2) a **API consumida** por esse build, que é
`https://api-hom.jrnexpenses.com` em vez de `https://api.jrnexpenses.com`.

## Critérios de aceite

- [x] Existe um bucket S3 próprio de homologação, servindo hosting
      estático via uma distribuição CloudFront própria (não a de
      produção) — `gastosapp-frontend-hom` + distribuição
      `ELE195A1APCLB` (`gastosapp-cdn-hom`)
- [x] Existe um certificado ACM válido para `hom.jrnexpenses.com`,
      anexado à distribuição de homologação — `ISSUED`, validado via
      DNS
- [x] `https://hom.jrnexpenses.com` responde servindo o conteúdo do
      bucket de homologação, via HTTPS, com certificado válido —
      validado (`200`, HTML do build de hom)
- [x] `https://jrnexpenses.com`/`https://www.jrnexpenses.com` (produção)
      continuam respondendo exatamente como antes desta feature, sem
      nenhuma regressão — validado (`200` em ambos, antes e depois do
      provisionamento de hom)
- [x] Um build publicado em homologação usa
      `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com` — validado via
      grep no bundle gerado (`npm run build:hom`); chamadas de API
      reais a partir do navegador ainda dependem de CORS no backend
      (ver "Status" abaixo)
- [x] Decisão sobre WAF na distribuição de homologação documentada e
      aprovada: WAF WebACL **dedicado** (mesmas 3 AWS Managed Rules de
      produção), coberto pelo plano flat-rate **Free** da distribuição
      (2º dos 3 disponíveis na conta) — custo US$0/mês, assinatura feita
      manualmente no console e confirmada pelo usuário
- [x] Nenhum recurso de homologação provisionado gera custo fixo por
      hora ligada; todo `terraform plan`/`apply` e o upload de teste ao
      S3 foram executados só após aprovação explícita do usuário no
      momento da execução
- [x] `frontend/infra/CLAUDE.md` atualizado para refletir a existência
      do ambiente de homologação e como ele se relaciona com produção

## Status

**Implementado, provisionado e validado.**

- `frontend/infra/terraform/environments/hom/` criado do zero (não
  import, diferente da FEAT-07): bucket S3 `gastosapp-frontend-hom`,
  WAF WebACL dedicado (`gastosapp-hom-web-acl`, mesmas 3 AWS Managed
  Rules de produção), OAC, certificado ACM `hom.jrnexpenses.com`
  (`ISSUED`), distribuição CloudFront `ELE195A1APCLB`
  (`gastosapp-cdn-hom`, `d15nea4q76w097.cloudfront.net`,
  `PriceClass_All`). 8 recursos no state.
- **Dependência circular encontrada e resolvida durante a execução**:
  como os recursos são criados do zero (não importados), o
  `terraform apply` inicial falhou na criação da distribuição
  (`InvalidViewerCertificate`) porque o certificado ACM nasce
  `PENDING_VALIDATION` e depende do CNAME de validação em `dns/`, que
  por sua vez normalmente viria depois da distribuição existir.
  Resolvido aplicando o CNAME de validação isoladamente (`-target`),
  aguardando o certificado virar `ISSUED`, e então completando o
  `apply` em `environments/hom/`. Passo a passo documentado em
  `frontend/infra/terraform/README.md` para reprodução futura.
- `frontend/infra/terraform/dns/` ganhou o segundo
  `terraform_remote_state` (desacoplado do de prod) e os 3 records de
  homologação (`hom_a`, `hom_aaaa`, `acm_validation_hom`) — os 6
  records de produção permaneceram inalterados.
- **Checkpoint manual concluído**: distribuição assinada pelo usuário
  ao plano flat-rate **Free** do CloudFront (2º dos 3 disponíveis na
  conta), confirmado via print do console (`Free plan ($0/month)`).
- `frontend/app/.env.hom.example` e script `build:hom` criados;
  `frontend/app/.gitignore` ajustado (`.env.hom.example` estava sendo
  ignorado por engano pelo padrão `.env.*` existente).
- Validação end-to-end: `npm run build:hom` gerado, confirmado via grep
  que o bundle só referencia `https://api-hom.jrnexpenses.com`; upload
  manual único (`aws s3 sync --delete`, aprovado pelo usuário) para
  `gastosapp-frontend-hom`; `https://hom.jrnexpenses.com` responde `200`
  servindo o SPA corretamente.
- Produção validada sem regressão antes e depois de todo o
  provisionamento (`https://jrnexpenses.com`/`https://www.jrnexpenses.com`
  respondendo `200` o tempo todo).
- **Gap de CORS resolvido em modo leve, fora desta feature** (contexto
  backend, conforme "Fora do escopo"): após a validação inicial, o
  usuário reportou erro de login em produção real; identificado que a
  origem `https://hom.jrnexpenses.com` nunca tinha sido liberada no
  CORS da API de homologação (parâmetro `/GastosApp/Hom/Cors/ProductionOrigins/0`
  não existia — a FEAT-13 backend antecedeu esta feature, não havia
  frontend de homologação ainda). Corrigido em
  `backend/infra/terraform/environments/hom/` (mesmo padrão da
  FEAT-11): `parameter-store.tf` (novo parâmetro) e `variables.tf`
  (`frontend_origins` default atualizado de `[]` para
  `["https://hom.jrnexpenses.com"]`, refletindo também no CORS do API
  Gateway). `terraform apply` (1 to add, 1 to change) aprovado e
  executado. Validado via `curl` (preflight `OPTIONS` e `POST
  /auth/login` retornando `access-control-allow-origin:
  https://hom.jrnexpenses.com`).
- **Gap conhecido, documentado**: a assinatura ao plano Free é manual
  hoje porque `aws_pricingplanmanager_subscription` ainda não existe em
  nenhuma versão publicada do `terraform-provider-aws`
  ([PR #49235](https://github.com/hashicorp/terraform-provider-aws/pull/49235),
  aberto e não mesclado). Trazer para o Terraform via `import` quando
  disponível.
- `frontend/infra/CLAUDE.md` e `frontend/infra/terraform/README.md`
  atualizados.
- Todos os comandos que tocaram a conta AWS real (`apply` em
  `environments/hom/` e `dns/`, upload de teste ao S3) foram aprovados
  explicitamente pelo usuário no momento da execução, conforme US6.

## Fora do escopo

- Ambiente de homologação do **backend** — já existe
  (`backend/specs/FEAT-13-ambiente-homologacao/`)
- Mudança de CORS na API de homologação para liberar
  `https://hom.jrnexpenses.com` — pertence ao contexto backend, feature
  futura separada nesse contexto
- Pipeline de CI/CD para deploy automático em homologação — o deploy
  (`npm run build` + upload para o bucket) continua manual, com
  aprovação passo a passo. Há intenção futura de criar uma stack de
  CI/CD própria, mas isso é feature separada, não parte desta spec
- Qualquer mudança de tela, rota ou comportamento do SPA — esta feature
  só duplica a infraestrutura que hospeda o mesmo código já existente
- `www.hom.jrnexpenses.com` ou qualquer alias adicional além de
  `hom.jrnexpenses.com` — não há necessidade identificada de variante
  `www` para um ambiente de homologação
