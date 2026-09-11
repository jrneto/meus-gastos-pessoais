# FEAT-39: Headers de observabilidade obrigatórios (trace-id, client-platform, client-version)

## Objetivo

Tornar obrigatórios os três headers de observabilidade que o frontend
já envia sem exceção em toda chamada de API — `trace-id`,
`client-platform` e `client-version` —, introduzidos como opcionais
pela FEAT-38. Requisições que não enviarem algum deles passam a ser
rejeitadas com 400, fechando parte do débito técnico registrado no
backlog original da FEAT-38.

## Contexto

Item do backlog (`backend/docs/backlog.md`, "DÉBITO — Headers de
observabilidade continuam opcionais"): a FEAT-38 introduziu quatro
headers de observabilidade — `trace-id`, `session-id`,
`client-platform`, `client-version` — todos opcionais, para não quebrar
o frontend da época, que ainda não os enviava. Desde então, o frontend
(`frontend/app/src/lib/httpClient.ts`) já envia `trace-id`,
`client-platform` e `client-version` em **toda** chamada, sem exceção
(inclusive `/auth/login`, `/auth/refresh` e as demais rotas de
autenticação) — não há mais motivo para mantê-los opcionais.

`session-id` fica de fora do escopo desta feature: ele só é gerado no
client depois de um login explícito bem-sucedido
(`frontend/app/src/lib/sessionId.ts`, `startNewSession`), então
`POST /auth/login` e o bootstrap silencioso via `POST /auth/refresh`
(recarregar a página, cookie httpOnly) são **sempre** chamados sem
`session-id`. Além disso, ele é tratado como best-effort no frontend —
pode voltar a faltar mesmo já autenticado, se `sessionStorage` não
estiver disponível (ex.: alguns modos de navegação privada). Exigi-lo
quebraria esses fluxos legítimos, então continua opcional (ver "Fora do
escopo").

**Decisões já confirmadas com o usuário durante o `/specify`:**

1. Só `trace-id`, `client-platform` e `client-version` tornam-se
   obrigatórios agora. `session-id` continua opcional indefinidamente —
   não é objetivo desta feature (fica como oportunidade de débito
   técnico a registrar quando esta feature for implementada).
2. `GET /health` fica isento da obrigatoriedade dos três headers —
   continua consultável via curl manual simples, sem headers
   customizados, preservando seu uso documentado hoje
   (`HealthEndpoints.cs`: checagem manual de versão em produção, sem
   exigir login).
3. `trace-id` perde o fallback de geração automática da FEAT-38 ("se o
   client não enviar, a API gera um valor novo") nas rotas não
   isentas — sua ausência passa a ser tratada como as dos outros dois
   headers: motivo de erro 400. Mesmo nesse caso de rejeição, a
   resposta 400 continua trazendo o header `trace-id` (gerado na hora,
   só para esse response), preservando o hábito de toda resposta ser
   correlacionável no log — inclusive quando o próprio motivo da
   rejeição é a ausência dele.

## Requisitos de negócio

- `trace-id`, `client-platform` e `client-version` passam a ser
  obrigatórios em toda rota da API, autenticada ou não, **exceto**
  `GET /health` — a ausência de qualquer um deles (header não enviado
  ou enviado vazio) resulta em erro 400, e a requisição não chega a ser
  processada (nenhuma regra de negócio do endpoint chega a rodar).
- `session-id` não muda de comportamento — continua totalmente
  opcional, exatamente como definido na FEAT-38.
- `GET /health` continua aceitando chamadas sem nenhum dos quatro
  headers, exatamente como hoje.
- Quando um ou mais dos três headers obrigatórios estiver ausente numa
  rota não isenta, a mensagem de erro identifica quais deles estão
  faltando (não só o primeiro problema encontrado) — evita que o client
  precise de várias tentativas para descobrir tudo que falta.
- `trace-id` deixa de ter fallback de geração automática nas rotas não
  isentas: se o client não enviar (ou enviar vazio), isso conta como
  header ausente e gera 400, junto com qualquer outro que também esteja
  faltando.
- Mesmo numa resposta 400 por header obrigatório ausente, o header de
  resposta `trace-id` continua presente — ecoando o valor do request
  quando ele foi enviado (mesmo que outro header esteja faltando), ou
  gerado pela API especificamente para aquele response, quando o
  próprio `trace-id` também estiver entre os ausentes.
- Nenhuma outra regra da FEAT-38 muda: log estruturado, payload
  completo em erro/log-level, redação de campos sensíveis e retenção de
  log group continuam como estão.

## User Stories

**US1 — Requisição com os três headers obrigatórios presentes**
- Given um client que envia `trace-id`, `client-platform` e
  `client-version` numa chamada a uma rota não isenta
- When a API recebe a requisição
- Then ela é processada normalmente (nenhuma mudança de comportamento
  em relação a hoje)

**US2 — Requisição sem `client-version` (exemplo de um header faltando)**
- Given um client que envia `trace-id` e `client-platform`, mas não
  envia `client-version`, numa chamada a uma rota não isenta
- When a API recebe a requisição
- Then ela responde 400 (`ProblemDetails`, RFC 9457) sem processar a
  regra de negócio do endpoint, o `detail` identifica `client-version`
  como o header ausente, e o response ainda ecoa o `trace-id` recebido
  no request

**US3 — Requisição sem nenhum dos três headers obrigatórios**
- Given um client que não envia `trace-id`, `client-platform` nem
  `client-version`, numa chamada a uma rota não isenta
- When a API recebe a requisição
- Then ela responde 400, o `detail` identifica os três headers como
  ausentes, e o response traz um `trace-id` gerado pela API
  especificamente para esse response (já que nenhum foi recebido)

**US4 — `GET /health` sem nenhum header**
- Given uma chamada a `GET /health` sem nenhum dos quatro headers de
  observabilidade
- When a API recebe a requisição
- Then ela responde normalmente (200), sem exigir nenhum dos headers —
  mesmo comportamento de hoje

**US5 — Requisição sem `session-id`**
- Given um client que envia `trace-id`, `client-platform` e
  `client-version`, mas não envia `session-id`, numa chamada a uma rota
  não isenta (ex.: `POST /auth/login`, ou qualquer chamada em modo de
  navegação privada onde `sessionStorage` não está disponível)
- When a API recebe a requisição
- Then ela é processada normalmente — `session-id` continua opcional,
  sem gerar erro 400

## Contratos da API

Mudança transversal, não um endpoint novo — aplica-se a toda rota já
existente e a qualquer rota futura, exceto `GET /health`.

**Request headers:**
- `trace-id: <string>` — obrigatório (exceto `GET /health`)
- `client-platform: <string>` — obrigatório (exceto `GET /health`)
- `client-version: <string>` — obrigatório (exceto `GET /health`)
- `session-id: <string>` — continua opcional (sem mudança da FEAT-38)

**Response headers:**
- `trace-id: <string>` — sempre presente em toda resposta (sucesso ou
  erro, inclusive o próprio 400 de header ausente), ecoando o valor
  recebido do client ou um valor gerado pela API quando ausente.

**Resposta de erro (400) por header obrigatório ausente:**

```json
{
  "status": 400,
  "title": "Parâmetros inválidos",
  "detail": "Header(s) obrigatório(s) ausente(s): client-version",
  "type": "https://gastosapp.dev/errors/missing-observability-headers"
}
```

(formato RFC 9457, `content-type: application/problem+json`, mesmo
padrão de `ResultHttpExtensions.BuildProblem` já usado no projeto;
`detail` lista todos os headers ausentes, separados por vírgula, quando
mais de um estiver faltando)

Nenhum endpoint existente muda de request/response body, status code
de sucesso ou `type` de erro de negócio já existente por causa desta
feature.

## Critérios de aceite

- [x] Toda rota da API, exceto `GET /health`, responde 400 quando
      `trace-id`, `client-platform` ou `client-version` estiver ausente
      (US2, US3)
- [x] O `detail` da resposta 400 identifica todos os headers
      obrigatórios ausentes na requisição, não só o primeiro (US2, US3)
- [x] `GET /health` continua funcionando sem exigir nenhum dos quatro
      headers (US4)
- [x] `session-id` continua não sendo exigido em nenhuma rota (US5)
- [x] Toda resposta, inclusive o 400 de header ausente, continua
      trazendo o header `trace-id` — ecoado do request quando presente,
      gerado pela API quando ausente (US2, US3)
- [x] Suíte de testes existente ajustada para continuar passando com os
      headers agora obrigatórios (unitário, componente, integrado)
- [x] `backend/docs/openapi.json` regenerado, caso a mudança seja
      representável no contrato OpenAPI gerado automaticamente pelo
      projeto (mesma ressalva já registrada pela FEAT-38: headers de
      middleware cross-cutting podem não ser representados pelo
      gerador)
- [x] Débito técnico atualizado em `backend/docs/backlog.md`: item
      atual sobre headers opcionais ("Headers de observabilidade
      continuam opcionais") refinado para refletir que só `session-id`
      permanece opcional, e por quê (login/refresh sem sessão ainda,
      modo de navegação privada)

## Status

Implementação concluída (todas as 19 tasks de `tasks.md`). Suíte
completa: 551 unit + 235 componente + 36 integrado (todos passando,
inclusive contra o binário Native AOT via `run-local.sh`).

**Desvios relevantes do plano original, encontrados durante a
implementação** (nenhum previsto em `plan.md`/`tasks.md`, todos
confirmados com o usuário antes de aplicar):

1. **Regressão em cascata na suíte de componente** — a primeira rodada
   da suíte completa acusou 188 de 235 testes falhando: qualquer teste
   de componente de qualquer módulo (Auth/Transactions/Categories/
   Members/Summary/Reports) que chamasse uma rota não isenta sem os 3
   headers passava a receber 400 antes de chegar no `TestAuthHandler`.
   Resolvido centralizando os headers como padrão em
   `ComponentTestWebApplicationFactory.ConfigureClient` (hook `protected
   virtual` do próprio `WebApplicationFactory<T>`) — zero edição nos
   testes já existentes.
   `RequestObservabilityMiddlewareTests` (que testa exatamente
   presença/ausência desses headers) passou a usar
   `factory.Server.CreateClient()` em vez de `factory.CreateClient()`,
   pulando esse hook de propósito.
2. **Mesmo problema na suíte integrada** — `IApiTransport.SendAsync`
   (`DirectHttpTransport`/`LambdaRieTransport`) não enviava nenhum
   header customizado; ~86 chamadas em 8 arquivos de teste ficariam
   quebradas. Resolvido do mesmo jeito (headers padrão nos dois
   transportes), com um novo parâmetro opcional
   `omitObservabilityHeaders` em `SendAsync` só para o teste que precisa
   simular a ausência de um header específico.
3. **`export-openapi.sh` também quebrava** — o próprio `curl` do script
   pra `/openapi/v1.json` (não é `/health`) recebia 400 em vez do
   contrato, chegando a corromper `openapi.json` com o corpo do erro
   (revertido antes de commitar). Fix trivial: `curl -H` com os 3
   headers. `openapi.json` regenerado depois confirmou vir **idêntico**
   ao anterior — a validação de middleware cross-cutting não é
   representada pelo gerador de OpenAPI do projeto, mesma ressalva já
   registrada pela FEAT-38.

Nenhum dos três desvios mudou o comportamento da API em si — todos
foram ajustes de infraestrutura de teste/tooling, necessários porque a
mudança de contrato afeta qualquer chamador que não envie os headers
(inclusive os da própria suíte do projeto).

## Fora do escopo

- Tornar `session-id` obrigatório — estruturalmente incompatível com
  `POST /auth/login`/`POST /auth/refresh` (chamados antes de existir
  sessão) e com o caráter best-effort do `sessionStorage` no frontend
  (ver "Contexto"). Continua registrado como débito técnico.
- Qualquer mudança de contrato (body, status code de sucesso, `type` de
  erro de negócio) dos endpoints já existentes, além da nova resposta
  400 por header ausente.
- Qualquer mudança nas regras de log estruturado, payload completo,
  redação de campos sensíveis ou retenção de log group já definidas
  pela FEAT-38.
- Front-end: nenhuma mudança é necessária no frontend web, que já
  envia os três headers em toda chamada; ajustes em futuros clients
  mobile/admin são responsabilidade de cada contexto de frontend, fora
  do escopo deste backlog de backend.
