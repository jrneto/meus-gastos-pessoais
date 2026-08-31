# Plan — FEAT-30: Categoria — escopar busca por ID (GSI2) por conta

Decisão de arquitetura central desta feature: a mudança de schema
(`GSI2PK` ganhando `accountId`) não pode ser um cutover instantâneo,
porque há dado real já gravado em homologação/produção (diferente de
toda migração anterior de `GSI2`/`GSI1`, que sempre pôde assumir tabela
vazia ou recriável). A estratégia abaixo (seção "Estratégia de deploy
sem downtime") separa "código novo no ar" de "backfill concluído" em
dois passos independentes, com uma leitura dupla temporária cobrindo o
intervalo entre os dois — para nunca haver uma categoria inacessível
por causa da ordem em que deploy e backfill acontecem.

## 1. Camadas afetadas

### Domain — `GastosApp.Domain`
Nenhuma mudança. `Category` continua sem saber nada sobre `GSI2PK` —
isso é decisão de Infrastructure.

### Application — `GastosApp.Application`
Nenhuma mudança de assinatura. `ICategoryRepository.GetByIdAsync`/
`UpdateAsync`/`DeleteAsync` já recebem `accountId` hoje — o bug é
inteiramente interno à implementação Infrastructure, que hoje ignora
esse `accountId` na busca por `GSI2` e só o usa depois, tarde demais,
num post-check.

### Infrastructure — `GastosApp.Infrastructure`

- **`Categories/CategoryItemMapper.cs`** — `BuildItem`: `GSI2PK` passa
  de `$"ID#{category.Id}"` para `$"ID#{category.AccountId}#{category.Id}"`.
  Único ponto de escrita do formato do item `Category` (compartilhado
  com o seed de `DynamoDbAccountRepository`, FEAT-28) — a mudança
  cobre os dois caminhos de escrita automaticamente, sem tocar em
  `DynamoDbAccountRepository.cs`.

- **`Categories/DynamoDbCategoryRepository.cs`**:
  - `LookupByIdAsync` ganha `accountId` como parâmetro (hoje só recebe
    `categoryId`) e passa a fazer **duas buscas em sequência** (ver
    "Decisões técnicas", item 1, para a justificativa da segunda):
    1. Query no `GSI2` por `GSI2PK = "ID#<accountId>#<categoryId>"`
       (formato novo) — precisa, sem ambiguidade possível, `Limit = 1`
       continua correto aqui porque a chave já é única por
       conta+categoria.
    2. Só se (1) não achou nada: Query no `GSI2` por
       `GSI2PK = "ID#<categoryId>"` (formato antigo/pré-backfill), sem
       `Limit`, filtrando em memória pelo item cujo `PK ==
       "ACCOUNT#<accountId>"`. Cobre categorias ainda não migradas
       pelo backfill sem reintroduzir o bug original: diferente do
       `Limit = 1` de hoje, aqui o item de outra conta nunca é aceito
       mesmo que a query da GSI2 devolva mais de um.
  - `GetByIdAsync`/`UpdateAsync`/`DeleteAsync`: passam `accountId` para
    `LookupByIdAsync` e **removem** o post-check `if (pk !=
    $"ACCOUNT#{accountId}") return null/NotFound` — fica redundante,
    já que `LookupByIdAsync` agora só devolve um item que já pertence
    à conta do chamador (formato novo por construção; formato antigo
    por filtro explícito no passo 2 acima).
  - `MapToCategory`: extração do `id` a partir do `GSI2PK` troca
    `IndexOf('#')` por `LastIndexOf('#')` — funciona sem distinção
    para os dois formatos (`ID#<id>` e `ID#<accountId>#<id>`), já que
    o `id` é sempre o último segmento.

- **Novo `backend/infra/scripts/backfill-category-gsi2.sh`** — script
  administrativo, rodado manualmente pelo usuário (fora de qualquer
  pipeline de CI/CD) contra cada ambiente com dado real, ver seção 3
  (decisão técnica 3) e seção 4.

### Api — `GastosApp.Api`
Nenhuma mudança — nenhum endpoint, request/response ou status code
muda (confirmado também no `spec.md`, seção "Contratos da API").

## 2. Contratos técnicos

### `LookupByIdAsync` (privado, `DynamoDbCategoryRepository`) — nova assinatura

```csharp
private async Task<(string Pk, string Sk)?> LookupByIdAsync(
    string accountId, string categoryId, CancellationToken cancellationToken)
```

Pseudocódigo (implementação exata fica pro `tasks.md`):

```csharp
var pk = $"ACCOUNT#{accountId}";

var primary = await Query(GSI2, "GSI2PK = :v", $"ID#{accountId}#{categoryId}", Limit: 1);
if (primary.Items.Count > 0)
    return (primary.Items[0]["PK"].S, primary.Items[0]["SK"].S);

// Fallback temporário — ver "Estratégia de deploy sem downtime".
var fallback = await Query(GSI2, "GSI2PK = :v", $"ID#{categoryId}"); // sem Limit
var match = fallback.Items.FirstOrDefault(i => i["PK"].S == pk);
return match is null ? null : (match["PK"].S, match["SK"].S);
```

### DynamoDB — `GSI2` de `Category` (entrada/saída)

| | Antes | Depois |
|---|---|---|
| `GSI2PK` gravado (`BuildItem`) | `ID#<categoryId>` | `ID#<accountId>#<categoryId>` |
| Query de busca (formato novo) | — | `GSI2PK = "ID#<accountId>#<categoryId>"`, `Limit=1` |
| Query de busca (fallback, formato antigo) | `GSI2PK = "ID#<categoryId>"`, `Limit=1`, **sem filtro de conta** (bug) | `GSI2PK = "ID#<categoryId>"`, sem `Limit`, filtrado em memória por `PK` esperado |

Nenhuma mudança na definição da `GSI2` em si (continua hash-key-only,
`GSI2PK`, `Projection: KEYS_ONLY`) — só no formato do valor gravado e
na forma de consultar. Sem impacto em `Transaction`, que continua
gravando/lendo `GSI2PK = "ID#<transactionId>"` (fora de escopo, ver
`spec.md`).

## 3. Decisões técnicas

**1. Leitura dupla temporária em `LookupByIdAsync` (formato novo
primeiro, formato antigo como fallback filtrado) em vez de tentar
sincronizar deploy do código com o backfill.** Alternativa considerada
e rejeitada: rodar o backfill e só depois fazer deploy do código (ou
vice-versa), assumindo que a ordem certa evita qualquer inconsistência.
Não evita: assim que o backfill atualiza o `GSI2PK` de um item, o
código **antigo ainda no ar** para de encontrar aquele item específico
(ele busca só o formato antigo) — o mesmo problema na direção oposta se
o deploy for primeiro e o backfill demorar. Como `GSI2PK` é um único
atributo (não dá pra gravar dois formatos ao mesmo tempo sem criar uma
GSI nova, rejeitado por exigir mudança de Terraform pra um problema
transitório), a leitura dupla é o jeito de fazer o código já novo
enxergar tanto o item ainda não migrado quanto o já migrado — deploy e
backfill deixam de ter qualquer relação de ordem ou timing entre si.
Custo aceito: uma segunda `Query` (sem `Limit`, mas em `GSI2`
`KEYS_ONLY`, portanto barata) só no caminho de categoria ainda não
migrada — desaparece à medida que o backfill avança, e o fallback
inteiro é removido do código depois que o backfill for confirmado
100% completo (ver "Pontos a confirmar").

**2. O fallback filtra em memória por `PK` esperado, em vez de repetir
o `Limit=1` que causa o bug original.** É a mesma "correção rápida"
que o `spec.md` explicitamente rejeitou como solução **permanente**
(custo de `Query` crescente por conta) — aqui ela é aceitável porque é
**transitória** (só existe enquanto durar a janela entre deploy e
backfill confirmado) e, ao contrário do `Limit=1` atual, **corrige** o
bug em vez de reproduzi-lo: só aceita o item cujo `PK` bate com a conta
do chamador, nunca "o primeiro que a query devolver".

**3. Backfill via `Scan` administrativo, rodado manualmente pelo
usuário — exceção explícita à regra "sem `Scan`" da constitution.**
Não há Query possível para enumerar "todas as categorias de todas as
contas" sem já saber os `accountId`s de antemão (nenhum GSI lista
contas ou categorias globalmente) — historicamente todo `Scan` proibido
pela constitution é sobre código de runtime da API (Lambda servindo
tráfego real, sob Free Tier), não um script administrativo rodado uma
única vez, offline, fora do código de produção publicado. Ainda assim,
por tocar dado real de produção, este ponto **precisa de confirmação
explícita do usuário antes do `/tasks`** (ver seção final) — mesmo
espírito de cautela já aplicado a qualquer recurso AWS com
custo/segurança.

**4. Script de backfill em bash + AWS CLI (`backend/infra/scripts/`),
não uma ferramenta C# nova.** Seguindo o único padrão de script já
estabelecido no repositório (`init-dynamodb.sh` etc.) em vez de criar
um projeto novo na solution só para uma operação de dado pontual.
`Scan` paginado (`--exclusive-start-key`) na tabela base, filtrando
`begins_with(SK, "CAT#")` (mesmo prefixo já usado por `ListAsync` —
identifica categoria independente do atributo `Tipo` estar presente ou
não, ao contrário de filtrar direto no `GSI2`, que é `KEYS_ONLY` e não
carrega `Tipo`); para cada item, `UpdateItem` trocando `GSI2PK` de
`ID#<id>` (extraído do item) para `ID#<accountId>#<id>` (`accountId`
extraído do próprio `PK` do item), com `ConditionExpression: GSI2PK =
:valorAntigo` — torna o script **idempotente e resumível** (rodar de
novo só re-processa o que ainda não migrou; itens já no formato novo
falham a condição silenciosamente e são contados como "já migrados",
não como erro) e evita sobrescrever uma escrita concorrente. Modo
`--dry-run` (só conta/lista, não escreve) obrigatório antes de rodar de
verdade em homologação/produção. Alternativa (ferramenta C# reusando
`IAmazonDynamoDB`/DI da aplicação) considerada e descartada por ora:
mais robusta a paginação/erros, mas sem nenhum precedente no repo e
peso desproporcional ao volume esperado de itens (escala de projeto
pessoal — dezenas a poucas centenas de categorias no total). Fica
registrado como alternativa caso o volume real em produção surpreenda.

**5. Remoção do fallback de `LookupByIdAsync` faz parte do escopo desta
mesma feature (não vira débito técnico à parte).** Depois do backfill
confirmado (script de verificação — mesmo `Scan` administrativo,
contando itens ainda em formato antigo, esperado zero — rodado como
último passo antes de considerar a feature concluída), uma tarefa final
do `tasks.md` remove o passo 2 de `LookupByIdAsync`, voltando a uma
única `Query` pelo formato novo. Evita deixar código morto/dívida sem
necessidade real de estender por outra feature — é baixo esforço e faz
parte do mesmo ciclo de implementação.

## 4. Estratégia de deploy sem downtime (resumo operacional)

1. Implementar e mergear o código com leitura dupla em
   `LookupByIdAsync` (novo formato primário + fallback formato antigo
   filtrado por conta) — a partir do deploy em cada ambiente, tanto
   categorias já migradas quanto ainda não migradas continuam
   acessíveis, sem relação de ordem com o passo 2.
2. Rodar `backfill-category-gsi2.sh --dry-run` e depois sem `--dry-run`
   em cada ambiente com dado real, na ordem: local (se houver dado
   persistido no LocalStack), homologação, produção — sem pressa, pode
   rodar minutos ou horas depois do deploy do passo 1, sem risco de
   janela de erro.
3. Rodar o mesmo script em modo de verificação (ou `--dry-run`, que já
   reporta quantos itens ainda estão no formato antigo) até confirmar
   zero itens pendentes em cada ambiente.
4. Só então, remover o fallback de `LookupByIdAsync` (decisão técnica
   5) e fazer o deploy final — este passo é o que efetivamente encerra
   a feature.

## 5. Recursos AWS usados ou afetados

**Nenhum recurso novo.** Reaproveita a tabela `GastosApp`/`GastosApp-Hom`/
`GastosApp-Local` e a `GSI2` já provisionadas — só o valor gravado em
`GSI2PK` muda, e o backfill é uma operação de dado (`Scan`+`UpdateItem`),
não uma mudança de schema Terraform. Nenhuma IAM Role nova: a execução
do script é manual, pelo usuário, usando suas próprias credenciais AWS
já configuradas localmente (mesmo perfil usado para qualquer operação
administrativa hoje) — não roda em CI/CD, não usa a Role
`gastosapp-backend-cicd`.

## 6. Erros de negócio → `ErrorType`/HTTP

Nenhum `Error`/`ErrorType` novo. O mapeamento já existente não muda —
`GetByIdAsync`/`UpdateAsync`/`DeleteAsync` continuam devolvendo
`null`/`NotFound` (404) quando a categoria realmente não existe na
conta do chamador; `POST`/`PUT /transactions` continuam devolvendo 400
`validation-error`/"Categoria inválida." pelo mesmo motivo. A única
mudança é que esses erros deixam de ocorrer **erroneamente** quando a
categoria existe de fato na conta do chamador.

## 7. Testes (visão geral — detalhamento fica pro `tasks.md`)

- `DynamoDbCategoryRepositoryTests`: atualizar todos os testes que
  montam item de teste com `GSI2PK` pra usar o formato novo; novo teste
  cobrindo o cenário de colisão (dois itens mock com `GSI2PK` no
  formato antigo — `ID#<id>` — mesmo `categoryId`, `PK`s de contas
  diferentes) confirmando que `GetByIdAsync`/`UpdateAsync`/`DeleteAsync`
  só enxergam o item da conta esperada, nunca o outro; teste do
  caminho feliz com item já no formato novo (sem precisar do
  fallback).
- Testes de componente (`CategoryEndpointsTests`,
  `TransactionEndpointsTests` para a validação de `categoryId`): sem
  mudança de contrato esperada, só confirmar ausência de regressão.
- Novo teste integrado cobrindo o repro exato da US1 do `spec.md`
  (duas contas, categoria padrão de mesmo id, `POST /transactions` na
  segunda conta não retorna mais 400) — API real, sem dublês.
- Regenerar `backend/docs/openapi.json`: sem diff esperado (confirma o
  "Contratos da API" do `spec.md`).
- Validação manual do backfill em homologação (rodar o script, conferir
  `GET /categories` e `POST /transactions` de uma conta convidada
  continuam funcionando antes/durante/depois) antes de repetir em
  produção.

## Pontos a confirmar antes do `/tasks`

1. **Exceção ao "sem `Scan`" da constitution para o script
   administrativo de backfill** (decisão técnica 3) — recomendado
   (offline, fora do runtime da API, sem alternativa viável), mas por
   tocar dado real de produção precisa de aprovação explícita do
   usuário antes de prosseguir.
2. **Script em bash + AWS CLI, não ferramenta C#** (decisão técnica 4)
   — recomendado por seguir o padrão já estabelecido no repo e pela
   escala pequena esperada; alternativa registrada caso o usuário
   prefira mais robustez.
3. **Remoção do fallback dentro da mesma feature** (decisão técnica 5)
   — recomendado; alternativa seria abrir como débito técnico
   separado no backlog caso o usuário prefira não estender o ciclo de
   implementação desta feature até a confirmação do backfill em
   produção.
4. **Ordem de execução do backfill entre ambientes** (seção 4, passo 2)
   — local → homologação → produção, só avançando após confirmar
   sucesso no anterior; confirmar se está de acordo ou se produção deve
   esperar um tempo de observação maior depois de homologação.
