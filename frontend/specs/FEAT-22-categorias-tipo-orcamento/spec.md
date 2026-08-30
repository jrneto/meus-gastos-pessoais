# FEAT-22: Categorias — tipo (despesa/receita) e orçamento mensal

## Objetivo

Estender o CRUD de categorias (FEAT-19, já migrado para o Modernist)
com os dois atributos que o backend já expõe desde a FEAT-21
(`backend/specs/FEAT-21-categoria-tipo-orcamento`): `tipo` (`despesa`
ou `receita`, obrigatório em toda categoria) e `orcamentoMensalCents`
(teto mensal, opcional, só faz sentido para categorias de despesa). A
tela de Categorias passa a separar as categorias em dois grupos —
Categorias de despesa e Categorias de receita — em vez da lista única
de hoje.

## Contexto

Hoje `features/categories` só conhece `nome`, `cor` e `icone`
(FEAT-13/FEAT-16 do backend, contrato antigo) — o formulário de nova/
editar categoria já nem expõe `cor`/`icone` na UI (só envia um valor
padrão por baixo dos panos, ver comentário em `CategoryForm.tsx`), mas
o tipo `CategoryItem` e o payload de escrita ainda os declaram. Desde a
FEAT-21 do backend, `cor`/`icone` **deixaram de existir no contrato**
(enviá-los não dá erro, só é ignorado) e `tipo` passou a ser
obrigatório em `POST`/`PUT /categories`; `orcamentoMensalCents` é
opcional (inteiro positivo em centavos, ou omitido/`null`).

O design (`frontend/design-system/README.md`, "Modelo de dados do
protótipo", e `frontend/design-system/web/jrnexpenses-web.dc.html`,
tela "Categorias e orçamentos") foi atualizado para refletir isso:
o formulário de nova categoria ganhou um seletor de tipo (Despesa/
Receita, mesmo padrão visual do seletor de role usado em Membros) e um
campo de "Teto mensal (R$)" que só aparece quando o tipo selecionado é
Despesa; a lista passou a ter duas seções — **Categorias de despesa**
(com etiqueta de tipo e teto mensal) e **Categorias de receita** (só
com etiqueta de tipo, sem teto). O screenshot `13-categorias-
orcamentos.png` ainda reflete uma versão anterior da tela (sem os dois
grupos) — o `.dc.html` é a fonte de verdade atual, conforme nota no
próprio `README.md` do design system.

**Decisões fechadas com o usuário durante este `/specify`:**

1. **Orçamento (teto mensal) só existe para categorias de despesa.**
   Categorias de receita não têm orçamento — o campo correspondente
   nem aparece no formulário quando o tipo selecionado é Receita.
2. **O indicador de consumo (barra gasto atual/orçamento, que o
   `.dc.html` já modela com `spentFmt`/`budgetFmt`/`barWidth`) fica
   fora do escopo desta feature**, mesmo o backend já expondo `GET
   /summary` (FEAT-23 do backend) com gasto por categoria. Motivo:
   evitar acoplar a feature de Categorias a um endpoint de outro
   domínio (Resumo) antes da hora — essa integração é retomada quando
   o frontend tiver sua própria feature de resumo/transações
   (`FEAT-26` do backlog do frontend, dashboard). Nesta feature, cada
   categoria de despesa mostra só o valor do teto definido (ou "Sem
   teto definido"), sem barra e sem gasto atual; categorias de receita
   não mostram nenhum valor (o "realizado" do design também depende da
   mesma integração futura).
3. **`cor`/`icone` são removidos por completo** do tipo `CategoryItem`,
   do payload de escrita e do formulário — não fazem mais parte do
   contrato desde a FEAT-21 do backend, e nenhuma tela depende deles
   hoje (confirmado por busca no código).
4. **Edição de categoria continua um único formulário** (nome + tipo +
   teto, quando aplicável), reaproveitando o fluxo de editar-em-linha
   já existente (ícone de lápis) — em vez de introduzir um segundo
   fluxo de "editar só o teto" como o `.dc.html` sugere (link "Editar
   teto" com input isolado). Simplificação deliberada: evita duas
   formas diferentes de editar a mesma categoria: o backend já exige o
   corpo completo em `PUT /categories/{id}` de qualquer forma, e o
   formulário completo já existe e funciona.
5. **Exclusão de categoria não muda** — continua disponível (ícone de
   lixeira), com o mesmo diálogo de confirmação já implementado
   (FEAT-19), para os dois tipos.

## Requisitos de negócio

- Toda categoria tem um `tipo`: `despesa` ou `receita` — campo
  obrigatório tanto para criar quanto para editar
- `orcamentoMensalCents` (teto mensal) só é aplicável a categorias de
  tipo `despesa`:
  - No formulário, o campo "Teto mensal (R$)" só é exibido quando o
    tipo selecionado é Despesa; ao trocar para Receita, o valor
    preenchido é descartado e o campo não é enviado
  - É opcional mesmo para despesa (categoria de despesa pode não ter
    teto definido)
  - Quando informado, deve ser um valor maior que zero — mensagem de
    validação client-side espelhando a regra do backend
- A lista de categorias é dividida em duas seções, nesta ordem:
  **Categorias de despesa** e **Categorias de receita**, cada uma só
  com as categorias do tipo correspondente
- Cada item da lista mostra o nome, uma indicação visual do tipo
  (etiqueta), e:
  - Despesa: o teto mensal formatado em reais quando definido, ou uma
    indicação de que não há teto definido
  - Receita: nenhum valor adicional (sem teto, sem indicador de
    consumo — ver "Decisões fechadas" item 2)
- Criar/editar/excluir categoria continuam exigindo o usuário
  autenticado ter role `Total` ou `Titular` na conta ativa (regra já
  aplicada pelo backend desde a FEAT-20; sem mudança de tratamento
  nesta feature além do que já existe)
- Erros da API mapeados como já são hoje (`ValidationError`,
  `NameConflictError`, `NotFoundError`, `NetworkError`,
  `UnknownCategoryError`) — `400` (`validation-error`) agora também
  cobre `tipo` ausente/inválido e `orcamentoMensalCents` inválido, sem
  necessidade de um erro tipado novo (o client já bloqueia esses casos
  antes do submit; `400` é só o fallback)

## User Stories

**US1 — Criar categoria de despesa sem teto**
- Given o formulário de nova categoria
- When o usuário preenche nome, seleciona tipo Despesa e não informa
  teto mensal, e submete
- Then `POST /categories` é chamado com `tipo: "despesa"` e
  `orcamentoMensalCents` omitido, a API retorna 201, e a categoria
  aparece na seção "Categorias de despesa" sem teto definido

**US2 — Criar categoria de despesa com teto**
- Given o formulário de nova categoria
- When o usuário preenche nome, seleciona tipo Despesa e informa um
  teto mensal válido (ex.: 800,00), e submete
- Then `POST /categories` é chamado com `orcamentoMensalCents: 80000`,
  a API retorna 201, e a categoria aparece na seção "Categorias de
  despesa" com o teto formatado em reais

**US3 — Criar categoria de receita**
- Given o formulário de nova categoria
- When o usuário preenche nome, seleciona tipo Receita, e submete (sem
  campo de teto disponível)
- Then `POST /categories` é chamado com `tipo: "receita"` e sem
  `orcamentoMensalCents`, a API retorna 201, e a categoria aparece na
  seção "Categorias de receita"

**US4 — Trocar de Despesa para Receita descarta o teto**
- Given o formulário de nova categoria com tipo Despesa selecionado e
  um teto mensal preenchido
- When o usuário troca o tipo para Receita
- Then o campo de teto mensal desaparece do formulário e seu valor não
  é enviado, mesmo se o usuário voltar para Despesa antes de submeter
  (valor descartado na troca)

**US5 — Editar categoria trocando o tipo**
- Given uma categoria de despesa existente, com teto definido
- When o usuário edita a categoria e troca o tipo para Receita, e
  submete
- Then `PUT /categories/{id}` é chamado com `tipo: "receita"` e sem
  `orcamentoMensalCents`, a API retorna 200, e a categoria passa a
  aparecer na seção "Categorias de receita"

**US6 — Editar teto de categoria de despesa existente**
- Given uma categoria de despesa existente, sem teto definido
- When o usuário edita a categoria, mantém o tipo Despesa e informa um
  teto mensal válido, e submete
- Then `PUT /categories/{id}` é chamado com o novo
  `orcamentoMensalCents`, a API retorna 200, e o teto passa a aparecer
  na lista

**US7 — Remover teto de categoria de despesa existente**
- Given uma categoria de despesa existente, com teto definido
- When o usuário edita a categoria, apaga o valor do campo de teto
  mensal (mantendo o tipo Despesa), e submete
- Then `PUT /categories/{id}` é chamado com `orcamentoMensalCents`
  omitido/`null`, a API retorna 200, e a categoria volta a aparecer sem
  teto definido

**US8 — Teto inválido bloqueado no client**
- Given o formulário de nova ou editar categoria com tipo Despesa
- When o usuário informa um teto mensal igual a zero, negativo, ou em
  formato inválido, e tenta submeter
- Then o submit é bloqueado no client, sem chamar a API, exibindo a
  mensagem de erro do campo

**US9 — Excluir categoria (sem mudança de comportamento)**
- Given uma categoria de qualquer tipo, sem despesas/receitas
  associadas
- When o usuário confirma a exclusão
- Then `DELETE /categories/{id}` é chamado, a API retorna 204/200, e a
  categoria some da seção correspondente (comportamento já existente,
  sem regressão)

## Contratos consumidos (já implementados no backend, sem mudança)

Ver contrato completo em
`backend/specs/FEAT-21-categoria-tipo-orcamento/spec.md`. Resumo do que
o frontend passa a enviar/receber:

### GET /categories

Response 200 (sem filtro por `tipo` nesta feature — a tela busca todas
as categorias da conta de uma vez e agrupa no client):
```json
{
  "items": [
    { "id": "...", "nome": "Alimentação", "tipo": "despesa", "orcamentoMensalCents": 80000, "createdAt": "2025-06-15T12:34:56Z" },
    { "id": "...", "nome": "Salário", "tipo": "receita", "orcamentoMensalCents": null, "createdAt": "2025-06-15T12:34:56Z" }
  ]
}
```

### POST /categories

Request (sem teto, categoria de receita):
```json
{ "nome": "Salário", "tipo": "receita" }
```
Request (com teto, categoria de despesa):
```json
{ "nome": "Alimentação", "tipo": "despesa", "orcamentoMensalCents": 80000 }
```
Response 201: mesmo formato do item de `GET /categories`.
Erros: `400` (`validation-error`), `403` (`insufficient-permission`),
`422` (`name-conflict`).

### PUT /categories/{id}

Mesmo corpo de `POST` (corpo completo). Response 200: mesmo formato.
Erros: `400`, `403`, `404` (`not-found`), `422` (`name-conflict`).

### DELETE /categories/{id}

Sem mudança nesta feature — `403` (`insufficient-permission`), `404`
(`not-found`), `422` (`category-in-use`).

## Critérios de aceite

- [ ] Formulário de nova categoria pede nome, tipo (Despesa/Receita) e,
      só quando tipo = Despesa, teto mensal opcional
- [ ] Trocar o tipo para Receita no formulário esconde e descarta o
      valor do teto mensal
- [ ] Criar categoria de despesa sem teto envia `orcamentoMensalCents`
      omitido e a categoria aparece na seção correta sem teto
- [ ] Criar categoria de despesa com teto válido envia o valor em
      centavos e a categoria aparece com o teto formatado
- [ ] Criar categoria de receita envia `tipo: "receita"` sem campo de
      teto
- [ ] Teto mensal igual a zero, negativo ou em formato inválido é
      bloqueado no client, sem chamar a API
- [ ] Editar categoria permite trocar o tipo, e a categoria passa a
      aparecer na seção correspondente ao novo tipo
- [ ] Editar categoria de despesa permite definir, atualizar ou remover
      o teto mensal (remover = enviar omitido/`null`)
- [ ] Lista de categorias é dividida em "Categorias de despesa" e
      "Categorias de receita", cada uma só com os itens do tipo
      correspondente
- [ ] Categorias de despesa mostram o teto mensal formatado (ou
      indicação de "sem teto"); categorias de receita não mostram
      nenhum valor monetário
- [ ] Exclusão de categoria continua funcionando para os dois tipos,
      sem regressão
- [ ] `cor`/`icone` removidos de `CategoryItem`, do payload de escrita
      e do formulário — nada no código os referencia mais
- [ ] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima

## Fora do escopo

- Indicador de consumo (barra gasto atual/orçamento) e "realizado" de
  categorias de receita — depende de `GET /summary` (backend FEAT-23),
  retomado quando o frontend tiver sua própria feature de resumo/
  transações (ver "Decisões fechadas" item 2)
- Filtro `GET /categories?tipo=` — esta feature busca todas as
  categorias de uma vez e agrupa no client; o parâmetro de query fica
  disponível para quando um seletor de categoria única (ex.: popup de
  nova receita) precisar filtrar por tipo
- Qualquer mudança no backend — `POST`/`PUT`/`GET`/`DELETE /categories`
  já implementam tudo que esta feature precisa
- Fluxo de "editar só o teto" separado do formulário completo (ver
  "Decisões fechadas" item 4)
- Aplicar `tipo` da categoria a `/expenses`/`/transactions` (ex.:
  impedir lançar despesa em categoria de receita) — feature separada
  (FEAT-23 do backlog do frontend)
