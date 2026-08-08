# FEAT-09: Esteira de CI/CD (GitHub Actions) para deploy do frontend

## Objetivo

Automatizar o deploy do frontend em homologação e produção via GitHub
Actions, substituindo o processo manual atual (`npm run build:hom` /
`npm run build` + `aws s3 sync` executados à mão, ver
`frontend/specs/FEAT-08-ambiente-homologacao/spec.md`, seção "Fora do
escopo"). O pipeline deve garantir qualidade (nenhum deploy com testes
quebrados), custo zero, e dar rastreabilidade de versão: cada build
publicado — em hom ou prod — deve ser identificável no próprio site e
essa identificação deve apontar para a release correspondente no
GitHub.

## Contexto

Hoje existem dois ambientes de hosting já provisionados via Terraform
(`frontend/infra/terraform/environments/{prod,hom}/`,
`frontend/specs/FEAT-07-terraform-import-infra/` e
`frontend/specs/FEAT-08-ambiente-homologacao/`):
`https://jrnexpenses.com` (produção) e `https://hom.jrnexpenses.com`
(homologação). O deploy de conteúdo (build do Vite + upload para o
bucket S3 correspondente) continua 100% manual — essa feature ataca
exatamente essa lacuna, sem alterar a infraestrutura de hosting já
existente.

O repositório GitHub (`jrneto/meus-gastos-pessoais`) era público e foi
recentemente tornado **privado**. Isso é relevante para o critério de
custo zero: repositório privado no plano GitHub Free tem cota de 2.000
minutos/mês de Actions gratuitos (runners hospedados pelo GitHub,
`ubuntu-latest`), diferente de repositório público (minutos
ilimitados). O pipeline deve ser desenhado para operar folgado dentro
dessa cota.

## Requisitos de negócio / restrições

- **Qualidade antes de publicar**: nenhum deploy (hom ou prod) pode
  acontecer se o build falhar ou se qualquer teste falhar. Aplica a
  mesma regra já vigente em `frontend/docs/constitution.md` ("nenhuma
  feature é considerada concluída com testes falhando") ao pipeline:
  lint/testes/build rodam antes de qualquer publicação, e uma falha em
  qualquer etapa interrompe o pipeline sem tocar no S3/CloudFront.
- **Deploy automático em homologação**: uma alteração integrada ao
  frontend (definição exata do gatilho — branch, PR merge, etc. — é
  decisão técnica de `plan.md`) dispara o pipeline; se as verificações
  de qualidade passarem, o novo build é publicado automaticamente em
  `https://hom.jrnexpenses.com`, sem intervenção manual.
- **Deploy em produção atrelado a uma release do GitHub**: a publicação
  em `https://jrnexpenses.com` acontece a partir de uma **GitHub
  Release** com tag de versão semântica (ex.: `v1.4.0`), não a cada
  push. O pipeline builda o código exatamente na tag da release e
  publica esse artefato em produção — a forma exata de disparo
  (workflow acionado por `release: published`, criação manual da tag,
  etc.) é decisão de `plan.md`.
- **Rastreabilidade de versão no próprio site**: tanto em hom quanto em
  prod, deve ser possível ver, na interface do SPA (ex.: rodapé ou tela
  "Sobre"), qual versão está publicada. Essa versão deve ser um link
  clicável para a release correspondente no GitHub
  (`github.com/jrneto/meus-gastos-pessoais/releases/tag/vX.Y.Z`). Em
  homologação — onde nem todo build corresponde a uma release
  formal — a versão exibida deve deixar claro do que se trata (ex.:
  identificador de commit/branch), sem quebrar o link/rastreabilidade
  para produção.
- **Custo zero**:
  - Nenhum runner self-hosted, nenhuma compra de minutos extras de
    Actions — o pipeline deve operar dentro da cota gratuita de 2.000
    min/mês do plano GitHub Free (repositório privado).
  - Nenhum novo recurso AWS com custo fixo por hora/instância ligada.
    A autenticação do pipeline na AWS (para publicar no S3 e invalidar
    cache do CloudFront) deve usar **OIDC** (GitHub Actions → IAM Role
    assumida via `aws-actions/configure-aws-credentials`), sem chave de
    acesso de longa duração armazenada em secret — reduz custo e risco
    de segurança ao mesmo tempo.
  - A criação da IAM Role/OIDC Provider na AWS — e qualquer outro
    recurso AWS novo que a esteira exija — segue a mesma regra já
    vigente (`frontend/docs/constitution.md`): **exige aprovação
    explícita do usuário antes de qualquer criação/alteração real**,
    nunca provisionado de forma autônoma.
- **Invalidação de cache**: após publicar novo conteúdo no bucket S3,
  o pipeline deve invalidar o cache da distribuição CloudFront
  correspondente (hom ou prod), para que a nova versão fique
  imediatamente visível — sem essa etapa, o comportamento observável
  do deploy automatizado seria pior que o processo manual atual (que já
  inclui esse cuidado implicitamente ao servir arquivos novos).
- **Isolamento entre ambientes preservado**: o pipeline nunca publica
  o build de homologação no bucket/distribuição de produção nem
  vice-versa — mesma garantia de isolamento já estabelecida na
  FEAT-08.
- **Nenhuma execução destrutiva/real sem aprovação explícita**: a
  criação de qualquer recurso AWS necessário para a esteira (IAM Role,
  OIDC Provider) segue a mesma regra já usada em specs anteriores de
  infra do frontend — nenhum `terraform apply`/comando equivalente
  roda de forma autônoma.

## User Stories

**US1 — Deploy automático em homologação com gate de qualidade**
- Given uma alteração do frontend integrada ao fluxo que dispara o
  pipeline de homologação
- When o pipeline roda lint/testes/build
- Then, se tudo passar, o novo build é publicado automaticamente em
  `https://hom.jrnexpenses.com`; se qualquer etapa falhar, nada é
  publicado e o pipeline reporta falha

**US2 — Deploy em produção a partir de uma release do GitHub**
- Given uma GitHub Release publicada com uma tag de versão semântica
  (ex.: `v1.4.0`)
- When o pipeline de produção é disparado por essa release
- Then, após lint/testes/build passarem para o código daquela tag, o
  build correspondente é publicado em `https://jrnexpenses.com`

**US3 — Rastreabilidade de versão em produção**
- Given um build publicado em produção a partir da release `vX.Y.Z`
- When um usuário acessa `https://jrnexpenses.com` e a área que exibe a
  versão (ex.: rodapé/"Sobre")
- Then ele vê a versão `vX.Y.Z` com um link que leva à release
  correspondente em `github.com/jrneto/meus-gastos-pessoais/releases`

**US4 — Rastreabilidade de versão em homologação**
- Given um build publicado em homologação (não necessariamente atrelado
  a uma release formal)
- When um usuário acessa `https://hom.jrnexpenses.com` e a área que
  exibe a versão
- Then ele vê um identificador que permite rastrear exatamente qual
  código está publicado (ex.: versão + commit), sem ambiguidade com o
  que está em produção

**US5 — Isolamento entre ambientes**
- Given os pipelines de homologação e produção configurados
- When um deploy de homologação roda
- Then ele nunca publica no bucket/distribuição de produção, e
  vice-versa

**US6 — Custo dentro da cota gratuita**
- Given o repositório privado no plano GitHub Free
- When os pipelines de hom e prod rodam ao longo de um mês de uso
  normal do projeto
- Then o consumo de minutos de Actions permanece dentro da cota
  gratuita (2.000 min/mês), sem uso de runner pago ou self-hosted

**US7 — Autenticação AWS sem credencial de longa duração**
- Given o pipeline precisa publicar no S3 e invalidar cache no
  CloudFront
- When ele se autentica na AWS
- Then usa uma IAM Role assumida via OIDC (GitHub Actions), sem chave
  de acesso (`AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`) de longa
  duração armazenada em secret do GitHub

**US8 — Nenhuma criação de recurso AWS sem aprovação**
- Given a necessidade de criar a IAM Role/OIDC Provider (ou qualquer
  outro recurso AWS novo) para a esteira funcionar
- When esse recurso está prestes a ser criado
- Then o usuário é consultado e aprova explicitamente antes de qualquer
  `apply`/criação real

## Contratos observáveis

Não há mudança no contrato de API consumido pelo frontend. As
mudanças observáveis são:
- **Novo elemento de UI**: exibição da versão publicada (texto +
  link), visível em qualquer tela (ex.: rodapé global) ou em uma tela
  dedicada ("Sobre") — decisão exata de onde/como fica para
  `plan.md`.
- **Novo comportamento de publicação**: pushes/releases passam a
  resultar em deploy automático, onde antes exigiam comando manual.

## Critérios de aceite

- [ ] Pipeline de homologação: dispara automaticamente a partir de uma
      alteração integrada ao frontend, roda lint/testes/build, e só
      publica em `hom.jrnexpenses.com` se tudo passar
- [ ] Pipeline de produção: dispara a partir de uma GitHub Release com
      tag semântica, roda lint/testes/build para o código da tag, e só
      publica em `jrnexpenses.com` se tudo passar
- [ ] Falha em qualquer etapa de qualidade (lint/teste/build) impede o
      deploy, em ambos os pipelines
- [ ] Após cada deploy bem-sucedido, o cache da distribuição CloudFront
      correspondente é invalidado
- [ ] O site em produção exibe a versão publicada (tag semântica) com
      link clicável para a release correspondente no GitHub
- [ ] O site em homologação exibe um identificador de versão/commit
      rastreável, distinto do de produção
- [ ] Deploy de homologação nunca afeta o bucket/distribuição de
      produção, e vice-versa (sem regressão em nenhum dos dois
      ambientes)
- [ ] Autenticação do pipeline na AWS feita via OIDC (IAM Role
      assumida), sem access key de longa duração em secret
- [ ] Nenhum recurso AWS novo (IAM Role, OIDC Provider) foi criado sem
      aprovação explícita do usuário no momento da execução
- [ ] Consumo de minutos de GitHub Actions, para o padrão de uso atual
      do projeto, fica confortavelmente dentro da cota gratuita de
      2.000 min/mês (repositório privado)
- [ ] Nenhum novo recurso AWS com custo fixo por hora/instância ligada
      foi introduzido

## Fora do escopo

- **CI/CD do backend** (deploy da API/Lambda) — feature futura
  separada nesse contexto, não faz parte desta spec
- **Ambientes efêmeros por Pull Request** (preview deployments) — não
  solicitado
- **Rollback automático** em caso de problema pós-deploy — não
  solicitado; rollback, se necessário, continua manual (republicar uma
  release anterior)
- **Notificações** (Slack, e-mail, etc.) sobre status do pipeline — não
  solicitado
- **Mudança na infraestrutura de hosting já provisionada**
  (bucket/distribuição/certificado) — esta feature só automatiza a
  publicação de conteúdo nela, não altera o que já existe
- **Geração automática de changelog/release notes** — a criação da
  GitHub Release e sua tag semântica é assumida como processo já
  existente/manual do usuário; esta spec não define como a release é
  criada, só como o pipeline reage a ela
