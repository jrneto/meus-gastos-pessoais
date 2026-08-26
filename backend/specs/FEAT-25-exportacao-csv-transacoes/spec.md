# FEAT-25: Exportação CSV de transações

## Objetivo

Expor `GET /transactions/export`, gerando um arquivo CSV com as
transações da conta ativa — reaproveitando os mesmos filtros já
suportados por `GET /transactions` (FEAT-22) — cobrindo o botão
"Exportar CSV" da tela Ajustes.

## Contexto

O design system (`frontend/design-system/screenshots/16-ajustes.png`)
mostra a tela "Ajustes" com uma linha "Exportar dados" e um botão
"Exportar CSV". Hoje não existe nenhuma rota que gere esse arquivo —
`GET /transactions` (FEAT-22) só retorna JSON paginado.

Segue `backend/docs/roadmap.md` (item "FEAT-25 — Exportação CSV de
transações").

**Escopo desta spec (revisado com o usuário antes de detalhar o
contrato):** durante o `/specify`, foi cogitado ampliar o escopo para
exportar também categorias e membros num arquivo Excel com múltiplas
abas. O usuário optou por **manter o escopo original do roadmap**: CSV,
somente transações. Fica registrado aqui para não se perder — se um dia
fizer sentido reabrir essa ideia (Excel multi-aba com categorias/
membros), ela exigiria uma nova spec própria e uma validação prévia de
compatibilidade da lib de geração de `.xlsx` com Native AOT (ver
`backend/docs/constitution.md`, que exige AOT pros cold starts do
Lambda) — bibliotecas comuns de Excel (ClosedXML, EPPlus) têm histórico
de problemas sob trimming/AOT.

**Decisões de escopo fechadas nesta spec:**

1. **Filtros**: `GET /transactions/export` aceita os mesmos filtros
   opcionais e combináveis de `GET /transactions` — `tipo`,
   `categoryId`, `yearMonth`, `dateFrom`, `dateTo`,
   `minAmountInCents`, `maxAmountInCents` — com as mesmas regras de
   validação já estabelecidas na FEAT-22. **Sem paginação**
   (`cursor`/`limit` não se aplicam): o CSV sempre traz o resultado
   completo da consulta filtrada, já que é pra download único, não
   pra navegação em tela.
2. **Colunas do CSV**, pensadas pra abrir direto numa planilha (não é
   um espelho do JSON de `GET /transactions`):
   - `data` (`YYYY-MM-DD`, mesmo formato ISO já usado na API)
   - `descricao`
   - `categoria` (**nome** da categoria, não o `categoryId` — mais
     útil pra quem abre a planilha; requer resolver o nome a partir do
     `categoryId` de cada transação)
   - `tipo` (`despesa`\|`receita`)
   - `valor` (em **reais, com vírgula decimal**, ex.: `45,90` — não em
     centavos; é a única exceção à convenção de "sempre centavos" do
     projeto, justamente porque este é o único ponto da API pensado
     pra consumo humano direto, não por outro sistema)
   - `lancadoPor` (mesmo texto de `createdByLabel` já usado em
     `GET /transactions`: "Você", nome do membro, ou "Ex-membro")
3. **Delimitador `;` (ponto e vírgula)**, não `,` — é o padrão do Excel
   configurado em `pt-BR` (que usa `,` como separador decimal); usar
   `,` como delimitador de coluna colidiria com o separador decimal de
   `valor`.
4. **Encoding UTF-8 com BOM** — necessário pro Excel reconhecer
   acentuação corretamente ao abrir o CSV direto (sem isso, caracteres
   acentuados aparecem corrompidos).
5. **Escaping RFC 4180**: campos que contenham `;`, `"` ou quebra de
   linha (principalmente `descricao`, texto livre) são envolvidos em
   aspas duplas, com aspas internas duplicadas (`"` → `""`).
6. **Sem resultado** (filtro não bate com nenhuma transação): retorna
   `200` com um CSV contendo só a linha de cabeçalho — mesmo padrão
   "sem 404 pra coleção vazia" já usado em `GET /transactions` e
   `GET /reports`.
7. **Acesso**: qualquer papel autenticado da conta ativa (`Leitura`,
   `Lancar`, `Total`, `Titular`) — mesma matriz de `GET /transactions`
   (leitura não é restrita por papel).
8. **Nome do arquivo fixo**: `Content-Disposition: attachment;
   filename="transacoes.csv"` — sem embutir filtros/data no nome; o
   navegador resolve duplicatas.

## Requisitos de negócio

- `GET /transactions/export` aceita os mesmos filtros opcionais de
  `GET /transactions` (`tipo`, `categoryId`, `yearMonth`, `dateFrom`,
  `dateTo`, `minAmountInCents`, `maxAmountInCents`), com as mesmas
  regras de validação (400 nos mesmos casos que já invalidam
  `GET /transactions`)
- Toda exportação é escopada à conta ativa do chamador (`accountId`
  resolvido do JWT, nunca do body/query) — nunca mistura dados de outra
  conta
- O CSV gerado contém uma linha por transação que casa com os filtros
  aplicados (todas, sem paginação), na ordem retornada pela consulta
- Coluna `categoria` traz o **nome** da categoria referenciada pela
  transação (`categoryId`), resolvido no momento da exportação
- Coluna `valor` é o `amountInCents` convertido para reais com vírgula
  decimal (ex.: `4590` centavos → `45,90`), nunca em centavos
- Coluna `lancadoPor` reaproveita o mesmo `createdByLabel` já calculado
  em `GET /transactions` (nome do membro, "Você" ou "Ex-membro")
- Delimitador de coluna é `;`; valores contendo `;`, `"` ou quebra de
  linha são escapados conforme RFC 4180
- Arquivo é gerado em UTF-8 com BOM
- Filtro sem nenhuma transação correspondente retorna `200` com CSV
  contendo somente a linha de cabeçalho (nunca 404)
- Qualquer papel autenticado da conta ativa pode exportar (sem
  restrição adicional de papel)

## User Stories

**US1 — Exportar todas as transações sem filtro**
- Given um usuário autenticado com transações de tipos e categorias
  diferentes na conta ativa
- When ele consulta `GET /transactions/export` sem nenhum filtro
- Then a API retorna 200 com um CSV contendo uma linha por transação da
  conta, com as colunas `data;descricao;categoria;tipo;valor;lancadoPor`

**US2 — Exportar filtrado por tipo**
- Given um usuário autenticado com despesas e receitas na conta ativa
- When ele consulta `GET /transactions/export?tipo=receita`
- Then o CSV retornado contém somente as transações com `tipo=receita`

**US3 — Exportar filtrado por categoria**
- Given um usuário autenticado com transações em categorias diferentes
- When ele consulta `GET /transactions/export?categoryId={id}`
- Then o CSV retornado contém somente transações dessa categoria, com a
  coluna `categoria` trazendo o nome correspondente a esse `categoryId`

**US4 — Exportar filtrado por período**
- Given um usuário autenticado com transações em meses diferentes
- When ele consulta `GET /transactions/export?yearMonth=2026-08` (ou
  `dateFrom`/`dateTo` equivalente)
- Then o CSV retornado contém somente transações com `date` dentro do
  período informado

**US5 — Exportar sem nenhum resultado**
- Given um usuário autenticado sem nenhuma transação que bata com o
  filtro aplicado
- When ele consulta `GET /transactions/export` com esse filtro
- Then a API retorna 200 com um CSV contendo somente a linha de
  cabeçalho (sem 404)

**US6 — Rejeitar filtro inválido**
- Given um usuário autenticado
- When ele consulta `GET /transactions/export?tipo=invalido` (ou
  qualquer outro filtro fora do formato aceito por `GET /transactions`)
- Then a API retorna 400 e nenhum arquivo é gerado

**US7 — Formatação de valor em reais com vírgula decimal**
- Given um usuário autenticado com uma transação de `amountInCents=4590`
- When ele consulta `GET /transactions/export`
- Then a coluna `valor` dessa linha traz `45,90` (não `4590`, não
  `45.90`)

**US8 — Escapar descrição com caractere especial**
- Given um usuário autenticado com uma transação cuja `description`
  contém `;` ou `"` (ex.: `Almoço; sobremesa "extra"`)
- When ele consulta `GET /transactions/export`
- Then a linha correspondente no CSV envolve o campo `descricao` em
  aspas duplas, com aspas internas duplicadas, conforme RFC 4180 — sem
  quebrar o parsing das colunas seguintes

**US9 — Isolamento entre contas**
- Given dois usuários autenticados em contas diferentes, cada um com
  suas próprias transações
- When cada um consulta `GET /transactions/export`
- Then o CSV de cada um contém apenas as transações da sua própria
  conta ativa

**US10 — Acesso liberado para qualquer papel**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele consulta `GET /transactions/export`
- Then a API retorna 200 normalmente (sem 403), mesmo comportamento
  para `Lancar`, `Total` e `Titular`

**US11 — Impedir exportação sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta `GET /transactions/export`
- Then a API retorna 401 e nenhum arquivo é gerado

## Contratos da API

### GET /transactions/export

Query params (todos opcionais, combináveis — mesmas regras de validação
de `GET /transactions`, sem `cursor`/`limit`):

| Param | Tipo | Formato |
|---|---|---|
| `tipo` | string | `despesa` \| `receita` |
| `categoryId` | string | id de uma categoria (não precisa existir — sem resultado, CSV só com cabeçalho) |
| `yearMonth` | string | `YYYY-MM` |
| `dateFrom` | string | `YYYY-MM-DD` |
| `dateTo` | string | `YYYY-MM-DD` |
| `minAmountInCents` | long | > 0 |
| `maxAmountInCents` | long | > 0 |

Response 200:
- `Content-Type: text/csv; charset=utf-8`
- `Content-Disposition: attachment; filename="transacoes.csv"`
- Corpo (UTF-8 com BOM, delimitador `;`, quebra de linha `\r\n`):
```csv
data;descricao;categoria;tipo;valor;lancadoPor
2026-08-15;Almoço no restaurante;Alimentacao;despesa;45,90;Você
2026-08-10;Salário;Renda;receita;5000,00;Você
```

Response 400 (validation-error): mesmas condições de `GET /transactions`
(ex.: `tipo` fora de `despesa`/`receita`, `dateFrom`/`dateTo`/
`yearMonth` fora do formato, `minAmountInCents`/`maxAmountInCents`
inválidos).
Response 401 (unauthorized).

Sem 403 (qualquer papel autenticado da conta ativa pode exportar), sem
404 (filtro sem resultado retorna 200 com CSV só de cabeçalho).

### Erros comuns a todas as rotas

Formato padrão de erro do projeto (`ResultHttpExtensions.BuildProblem`):
`title` fixo e genérico por tipo de erro (RFC 9457), mensagem
específica sempre em `detail`. Fonte de verdade exata:
`backend/docs/openapi.json`.

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "O parâmetro tipo deve ser despesa ou receita."
}
```

Response 401 (unauthorized):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}
```

## Critérios de aceite

- [ ] `GET /transactions/export` sem filtro retorna 200 com um CSV
      contendo uma linha por transação da conta ativa
- [ ] Filtros `tipo`, `categoryId`, `yearMonth`, `dateFrom`, `dateTo`,
      `minAmountInCents`, `maxAmountInCents` funcionam combinados,
      restringindo as linhas do CSV às transações correspondentes
- [ ] Filtro inválido (mesmas regras de `GET /transactions`) retorna 400
      e nenhum arquivo é gerado
- [ ] Filtro sem nenhuma transação correspondente retorna 200 com CSV
      contendo somente a linha de cabeçalho (nunca 404)
- [ ] Coluna `categoria` traz o nome da categoria (não o `categoryId`)
- [ ] Coluna `valor` traz o valor em reais com vírgula decimal (ex.:
      `45,90`), nunca em centavos
- [ ] Coluna `lancadoPor` reflete o mesmo `createdByLabel` de
      `GET /transactions` ("Você", nome do membro ou "Ex-membro")
- [ ] Delimitador de coluna é `;`; campos com `;`, `"` ou quebra de
      linha são escapados conforme RFC 4180
- [ ] Arquivo é servido com `Content-Type: text/csv; charset=utf-8` e
      `Content-Disposition: attachment; filename="transacoes.csv"`,
      codificado em UTF-8 com BOM
- [ ] Dados de uma conta nunca aparecem na exportação de outra conta
- [ ] Qualquer papel autenticado (`Leitura`, `Lancar`, `Total`,
      `Titular`) recebe 200 em `GET /transactions/export`
- [ ] Requisição sem token JWT válido retorna 401
- [ ] `backend/docs/openapi.json` regenerado refletindo o novo endpoint
      `GET /transactions/export` (parâmetros, `200` com
      `text/csv`, `400`/`401`)

## Fora do escopo

- Exportação de categorias e membros — cogitada durante o `/specify`,
  descartada por decisão do usuário; ver nota em "Contexto" acima
- Formato Excel (`.xlsx`) ou qualquer formato além de CSV
- Paginação da exportação — sempre traz o resultado completo do filtro
  aplicado numa única resposta
- Exportação assíncrona/em background (ex.: gerar arquivo grande via
  job e notificar por e-mail) — mesma decisão de "sem tabela agregada/
  sem pré-processamento" já usada em `/summary` e `/reports`: cálculo
  sempre síncrono, na própria request
- Agendamento de exportação recorrente
