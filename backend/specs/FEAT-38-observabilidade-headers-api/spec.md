# FEAT-38: Observabilidade — trace-id, session-id e client-platform nos headers de API

## Objetivo

Introduzir três headers de observabilidade em toda chamada de API —
`trace-id`, `session-id`, `client-platform`/`client-version` —
propagados e logados de forma estruturada, permitindo reconstruir
tanto uma requisição isolada quanto a jornada completa de uma sessão
de usuário através de múltiplos clients (web, mobile, admin).

## Contexto

Item do backlog (`backend/docs/backlog.md`). Hoje os logs não têm
correlação entre si — debugar um erro relatado por um usuário exige
garimpo manual no CloudWatch, sem nenhum identificador comum entre as
linhas de log de uma mesma requisição, nem entre requisições de uma
mesma sessão de uso. Com a expansão planejada para apps nativos
(Android/iOS) e painel administrativo, múltiplos clients vão bater na
mesma API simultaneamente, tornando essa lacuna mais custosa. Resolver
isso agora, antes de multiplicar clients, evita retrofitting caro
depois.

**Decisões já confirmadas com o usuário durante o `/specify`:**

1. **Toggle de log de payload completo é só global nesta feature** —
   o backlog original cogitava algo "idealmente por sessão específica,
   não globalmente" (ex.: ligar log verboso só pra investigar a sessão
   de um usuário específico, sem poluir o log de todo mundo). Essa
   segmentação por `session-id` fica fora do escopo da FEAT-38 —
   registrada como débito técnico em `backend/docs/backlog.md`. Aqui
   entra só um parâmetro de log-level **global** no Parameter Store
   (liga/desliga payload completo pra toda a API, em todo ambiente que
   o parâmetro cobrir).
2. **Sem propagação de `trace-id` para a Lambda de triggers do Cognito**
   (`GastosApp.CognitoTriggers`) nesta feature — mesmo quando o
   request que dispara indiretamente o trigger (registro, confirmação,
   `forgot-password`) já carrega um `trace-id`. Correlacionar as duas
   Lambdas exigiria propagar o `trace-id` via `ClientMetadata` nas
   chamadas ao Cognito e lê-lo do lado do trigger — fica registrado
   como débito técnico/melhoria futura. FEAT-38 cobre só a Lambda da
   API principal.
3. **Mudanças de infraestrutura (Terraform) entram no escopo da spec/
   plan** — retenção explícita de log group (7 dias hom, 15 dias prod)
   e o parâmetro de log-level no Parameter Store. Segue o fluxo normal
   do projeto: a aprovação explícita do usuário para o `terraform
   apply` de fato acontece só no momento da implementação, não aqui.

## Nomenclatura

Headers em minúsculo, separados por traço, sem prefixo `X-` —
alinhado com a RFC 6648 (que descontinuou `X-` em headers
customizados) e com o padrão de fato em HTTP/2+, onde headers
trafegam em minúsculo no wire format.

## Requisitos de negócio

- Quatro headers de request, todos **opcionais** (não tornar
  obrigatórios agora, para não quebrar o frontend atual — ver débito
  técnico correspondente abaixo, para torná-los obrigatórios no
  futuro, depois dos ajustes necessários no frontend):
  - `trace-id`: identifica uma requisição isolada.
  - `session-id`: identifica a sessão de uso (UUID gerado no client no
    momento do login bem-sucedido; enviado em toda chamada durante a
    vida da sessão). Conceito de aplicação, independente do token JWT
    do Cognito — refresh de token não gera novo `session-id`, só um
    novo login gera.
  - `client-platform`: identifica a origem da chamada (ex.: `web`,
    `android`, `ios`, `admin-web`). Sem lista fechada de valores
    válidos nesta feature — qualquer string é aceita e logada como
    veio, para não travar clients futuros ainda não previstos.
  - `client-version`: versão do client que fez a chamada. Sem
    validação de formato.
- Aplica-se a **toda rota da API**, autenticada ou não (inclusive
  `/health` e os endpoints de `/auth/*` que não exigem login).
- `trace-id`: se o client não enviar, a API gera um valor novo para a
  requisição. Em ambos os casos (recebido do client ou gerado pela
  API), o mesmo valor é sempre devolvido no header `trace-id` da
  resposta — inclusive em respostas de erro (4xx/5xx).
- `session-id`, `client-platform` e `client-version` **não** são
  gerados pela API quando ausentes, e não são ecoados na resposta —
  servem só para enriquecer o log da requisição; quando ausentes, o
  log correspondente registra o campo vazio/nulo, sem bloquear a
  chamada.
- Todo request gera uma linha de log estruturado padrão, compatível
  com consulta via CloudWatch Logs Insights, contendo pelo menos:
  `trace-id`, `session-id`, `client-platform`, `client-version`,
  `userId` (quando disponível a partir do JWT) e metadados da
  requisição (rota, método, status code, duração) — **sem** o corpo
  completo da requisição/resposta.
- Payload completo (corpo da requisição/resposta) é logado
  automaticamente sempre que a resposta for um erro (4xx ou 5xx),
  independentemente do toggle de log-level.
- Fora de cenário de erro, o log do payload completo só acontece
  quando um parâmetro de log-level global, lido do Parameter Store,
  estiver ativado — permite ligar temporariamente log verboso para
  depuração manual, sem exigir redeploy. Desligado é o padrão.
- Campos sensíveis (senha, token de autenticação, dados de cartão, e
  qualquer outro campo já tratado como sensível hoje no projeto) nunca
  são logados, mesmo com o payload completo ativado — truncados/
  redigidos antes de qualquer log.
- Log groups da API (produção e homologação) passam a ter retenção
  explícita configurada (nunca "Never Expire"): 7 dias em homologação,
  15 dias em produção.

## User Stories

**US1 — Requisição com todos os headers de observabilidade**
- Given um client que já fez login e guardou um `session-id`
- When ele chama qualquer endpoint da API enviando `trace-id`,
  `session-id`, `client-platform` e `client-version`
- Then a API responde normalmente, ecoa o mesmo `trace-id` no header
  da resposta, e a linha de log da requisição traz os quatro valores
  recebidos (+ `userId`, quando autenticado)

**US2 — Requisição sem nenhum header de observabilidade**
- Given um client que não envia nenhum dos quatro headers (ex.:
  frontend atual, antes do ajuste correspondente)
- When ele chama qualquer endpoint da API
- Then a API responde normalmente (nenhum dos headers é obrigatório),
  gera um `trace-id` novo e o devolve no header da resposta, e a linha
  de log registra `session-id`/`client-platform`/`client-version`
  vazios

**US3 — `trace-id` também aparece em resposta de erro**
- Given uma chamada que resulta em erro (validação, erro de negócio ou
  exceção não tratada)
- When a API responde com status 4xx ou 5xx
- Then o header `trace-id` da resposta traz o mesmo valor da
  requisição (ou o gerado pela API, se o client não enviou), e a linha
  de log da requisição inclui o payload completo (corpo da
  requisição), mesmo com o toggle de log-level desligado

**US4 — Log-level verboso ligado via Parameter Store**
- Given o parâmetro global de log-level no Parameter Store ativado
- When qualquer requisição bem-sucedida (2xx) é feita
- Then a linha de log correspondente inclui também o payload completo
  da requisição/resposta, além dos campos padrão — exceto campos
  sensíveis, que continuam nunca sendo logados

**US5 — Log-level desligado (padrão)**
- Given o parâmetro global de log-level no Parameter Store desligado
  (ou nunca configurado)
- When uma requisição bem-sucedida (2xx) é feita
- Then a linha de log correspondente traz só os campos padrão (sem
  payload completo)

## Contratos da API

Mudança transversal, não um endpoint novo — aplica-se a toda rota já
existente e a qualquer rota futura.

**Request headers (todos opcionais, em qualquer endpoint):**
- `trace-id: <string>`
- `session-id: <string>`
- `client-platform: <string>`
- `client-version: <string>`

**Response headers (toda resposta, inclusive erros):**
- `trace-id: <string>` — sempre presente; ecoa o valor recebido do
  client, ou um valor gerado pela API quando o client não enviou.

Nenhum endpoint existente muda de request/response body, status code
ou `type` de erro por causa desta feature.

## Critérios de aceite

- [ ] Qualquer chamada a qualquer endpoint aceita os quatro headers
      opcionais sem exigir nenhum deles (US1, US2)
- [ ] Toda resposta da API (sucesso ou erro) inclui o header
      `trace-id`, ecoando o valor recebido ou um valor gerado pela API
      quando ausente (US1, US2, US3)
- [ ] Toda requisição gera uma linha de log estruturado com `trace-id`,
      `session-id`, `client-platform`, `client-version` e `userId`
      (quando disponível), consultável via CloudWatch Logs Insights
      (US1, US2)
- [ ] Resposta de erro (4xx/5xx) sempre loga o payload completo da
      requisição, independentemente do toggle de log-level (US3)
- [ ] Payload completo de requisição bem-sucedida (2xx) só é logado
      com o parâmetro global de log-level ativado no Parameter Store
      (US4, US5)
- [ ] Campos sensíveis (senha, token, dados de cartão) nunca aparecem
      no log, mesmo com payload completo ativado (US4)
- [ ] Log groups de produção e homologação da API com retenção
      explícita (15 dias prod, 7 dias hom), nunca "Never Expire"
- [ ] Nenhum endpoint existente exige os headers novos — suíte de
      testes existente (unitário, componente, integrado) continua
      passando sem alteração de contrato
- [ ] Débito técnico registrado em `backend/docs/backlog.md`: tornar
      os quatro headers obrigatórios no futuro, após os ajustes
      necessários no frontend (decisão 1 do backlog original)
- [ ] Débito técnico registrado em `backend/docs/backlog.md`: log de
      payload completo segmentado por `session-id` específico, não só
      globalmente (decisão 1 do `/specify`)
- [ ] Débito técnico registrado em `backend/docs/backlog.md`: propagar
      `trace-id` para a Lambda de triggers do Cognito via
      `ClientMetadata`, correlacionando as duas Lambdas (decisão 2 do
      `/specify`)
- [ ] `backend/docs/openapi.json` regenerado, caso a mudança de
      headers seja representável no contrato OpenAPI gerado
      automaticamente pelo projeto

## Fora do escopo

- Tornar os headers obrigatórios (fica como débito técnico, ver acima)
- Log de payload completo segmentado por `session-id` específico (fica
  como débito técnico, ver acima)
- Propagação de `trace-id` para a Lambda de triggers do Cognito
  (`GastosApp.CognitoTriggers`) via `ClientMetadata` (fica como débito
  técnico, ver acima)
- Qualquer mudança de contrato (body, status code, `type` de erro) dos
  endpoints já existentes
- Rate limiting, autenticação ou qualquer controle de acesso baseado
  nos headers de observabilidade — são só para correlação de log
- Front-end: geração/envio de `trace-id`/`session-id`/
  `client-platform`/`client-version` pelos clients (web, futuros
  mobile/admin) é responsabilidade de cada contexto de frontend, fora
  do escopo deste backlog de backend
