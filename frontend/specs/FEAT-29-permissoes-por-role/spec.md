# FEAT-29: Permissões por role na UI

## Objetivo

Fazer a UI de Transações e Categorias esconder/desabilitar ações de
escrita conforme o papel (`role`) do usuário logado na conta ativa, e
tratar de forma amigável um eventual 403 que ainda assim chegue da API
— hoje a UI ignora completamente o papel do usuário: qualquer pessoa
autenticada vê todos os botões de lançar/editar/excluir despesa,
receita e categoria, mesmo quando o backend vai recusar a ação com 403.

## Contexto

O backend já aplica a matriz de autorização por papel desde a
FEAT-20 (`backend/specs/FEAT-20-membros-convites-permissoes/spec.md`),
atualizada pela FEAT-22 para a regra de autoria de transações
(`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`) — nenhum
contrato novo nasce nesta feature, é só o frontend passar a refletir o
que a API já decide. A FEAT-28 (Membros) já resolveu esse tratamento
para a própria tela de Membros (interface completa só pro Titular,
somente leitura pra qualquer outro papel — decisão 1 do
`frontend/specs/FEAT-28-membros-convites/spec.md`) e deixou
explicitamente de fora "Tratamento fino de permissão por role nas
telas de Transações/Categorias (...) — escopo da FEAT-29".

Os 4 papéis (`Leitura`, `Lancar`, `Total`, `Titular`) e a matriz de
autorização vigente nos dois recursos afetados por esta feature:

| Ação | Leitura | Lancar | Total | Titular |
|---|:-:|:-:|:-:|:-:|
| `GET /transactions` (listar/consultar) | ✅ | ✅ | ✅ | ✅ |
| `POST /transactions` (criar despesa/receita) | 403 | ✅ | ✅ | ✅ |
| `PUT`/`DELETE /transactions/{id}` — transação **própria** (`createdByUserId` = chamador) | 403 | ✅ | ✅ | ✅ |
| `PUT`/`DELETE /transactions/{id}` — transação de **outro membro** | 403 | 403 | ✅ | ✅ |
| `GET /categories` (listar) | ✅ | ✅ | ✅ | ✅ |
| `POST`/`PUT /categories` (criar/editar, incl. orçamento) | 403 | 403 | ✅ | ✅ |
| `DELETE /categories` | 403 | 403 | ✅ | ✅ |

Não existe hoje nenhum endpoint que devolva "qual é o meu papel" (o
próprio `GET /auth/me` não inclui `role` — ver
`frontend/specs/FEAT-28-membros-convites/spec.md`, decisão 3): a única
forma já usada no frontend é `GET /members` + `GET /auth/me` em
paralelo, cruzando pelo e-mail (mesma mecânica da tela de Membros).
Hoje essa mecânica só roda dentro da feature `members`; esta feature
precisa do mesmo dado nas telas de Transações e Categorias também —
como resolver isso (nova chamada por tela vs. estado compartilhado) é
decisão de implementação, não desta spec.

Telas afetadas (nenhuma mudança visual além de esconder/desabilitar
controles já existentes — sem novo layout no design system):
- **Transações** (`frontend/app/src/routes/TransactionsListPage.tsx`):
  botões "+ Nova despesa"/"+ Nova receita", e os botões "Editar"/
  "Excluir" do popup de detalhe de uma transação
  (`TransactionDetailDialog`)
- **Categorias** (`frontend/app/src/routes/CategoriesPage.tsx`): botão
  "+ Nova categoria", e os ícones de editar/excluir de cada linha
  (`CategoryList`) — o orçamento mensal é só mais um campo do mesmo
  formulário de criar/editar categoria (FEAT-21), não tem controle de
  UI próprio

Hoje, tanto `features/transactions` quanto `features/categories`
tratam qualquer 403 vindo da API caindo no erro genérico já existente
(`UnknownTransactionError`/`UnknownCategoryError`, mensagem "Ocorreu um
erro inesperado. Tente novamente.") — diferente da feature `members`,
que já tem um `ForbiddenError` próprio desde a FEAT-28.

## Requisitos de negócio

- Toda ação de escrita (criar despesa/receita, editar/excluir uma
  transação, criar/editar/excluir uma categoria) só é oferecida como
  botão/controle na UI quando o papel do usuário logado na conta ativa
  permite essa ação, segundo a matriz acima — nenhuma ação escondida
  fica só desabilitada com um motivo visível; ela simplesmente não
  aparece (mesmo padrão já adotado na FEAT-28 para a tela de Membros)
- Papel `Leitura`: nunca vê nenhum botão de escrita em Transações
  (criar despesa/receita, editar/excluir) nem em Categorias (criar,
  editar, excluir, incluindo o campo de orçamento) — só consulta
- Papel `Lancar`: vê os botões de criar despesa/receita; no popup de
  detalhe de uma transação, só vê "Editar"/"Excluir" quando ele mesmo
  criou aquela transação (`createdByUserId` igual ao próprio usuário) —
  nas transações lançadas por outro membro da conta, esses dois botões
  não aparecem; em Categorias, não vê nenhum botão de escrita (mesmo
  tratamento do papel `Leitura` nesse recurso, já que `Lancar` não tem
  permissão de escrita em categorias)
- Papéis `Total` e `Titular`: acesso irrestrito em Transações (criar,
  editar e excluir qualquer transação, independente de quem a criou) e
  em Categorias (criar, editar e excluir, incluindo o orçamento) — sem
  nenhuma ação escondida
- Mesmo com a ação escondida de antemão, uma chamada que ainda assim
  retornar 403 (ex.: o papel do usuário foi rebaixado pelo Titular em
  outra sessão entre a tela carregar e a ação ser confirmada) precisa
  ser tratada com uma mensagem específica de acesso negado — nunca cair
  no erro genérico atual ("Ocorreu um erro inesperado. Tente
  novamente.")
- Enquanto o próprio papel do usuário ainda não foi determinado (dados
  ainda carregando), nenhum botão de escrita é exibido — evita um
  flash de botão que desaparece logo em seguida

## User Stories

**US1 — Leitura não vê nenhuma ação de escrita em Transações**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele abre a tela "Transações"
- Then não vê os botões "+ Nova despesa"/"+ Nova receita"; ao abrir o
  detalhe de qualquer transação, não vê "Editar" nem "Excluir"

**US2 — Leitura não vê nenhuma ação de escrita em Categorias**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele abre a tela "Categorias e orçamentos"
- Then não vê o botão "+ Nova categoria", nem os ícones de editar/
  excluir em nenhuma categoria da lista

**US3 — Lancar vê os botões de criar despesa/receita**
- Given um usuário autenticado com papel `Lancar` na conta ativa
- When ele abre a tela "Transações"
- Then vê os botões "+ Nova despesa" e "+ Nova receita" habilitados

**US4 — Lancar só edita/exclui a própria transação**
- Given um usuário autenticado com papel `Lancar`, com uma transação
  criada por ele mesmo e outra criada por outro membro da conta
- When ele abre o detalhe de cada uma
- Then vê "Editar"/"Excluir" na transação que ele criou, e não vê
  nenhum dos dois botões na transação do outro membro

**US5 — Lancar não vê nenhuma ação de escrita em Categorias**
- Given um usuário autenticado com papel `Lancar` na conta ativa
- When ele abre a tela "Categorias e orçamentos"
- Then não vê o botão "+ Nova categoria", nem os ícones de editar/
  excluir em nenhuma categoria da lista (mesmo comportamento do papel
  `Leitura` neste recurso)

**US6 — Total/Titular tem acesso irrestrito em Transações**
- Given um usuário autenticado com papel `Total` ou `Titular`, com uma
  transação criada por outro membro da conta
- When ele abre a tela "Transações" e o detalhe dessa transação
- Then vê "+ Nova despesa"/"+ Nova receita", e "Editar"/"Excluir"
  habilitados, mesmo a transação não sendo dele

**US7 — Total/Titular tem acesso irrestrito em Categorias**
- Given um usuário autenticado com papel `Total` ou `Titular`
- When ele abre a tela "Categorias e orçamentos"
- Then vê o botão "+ Nova categoria" e os ícones de editar/excluir em
  toda categoria, incluindo o campo de orçamento no formulário

**US8 — 403 defensivo ao editar/excluir uma transação**
- Given um usuário autenticado cuja chamada de escrita em
  `/transactions` retorna 403 (papel insuficiente ou perda de posse da
  transação, mesmo com o botão correspondente já escondido pela UI
  antes desse cenário acontecer)
- When a chamada falha com 403
- Then a UI mostra uma mensagem específica de acesso negado ("Seu nível
  de acesso não permite esta ação."), nunca o erro genérico atual

**US9 — 403 defensivo ao criar/editar/excluir uma categoria**
- Given um usuário autenticado cuja chamada de escrita em
  `/categories` retorna 403
- When a chamada falha com 403
- Then a UI mostra a mesma mensagem específica de acesso negado, nunca
  o erro genérico atual

**US10 — Nenhum botão de escrita aparece durante o carregamento**
- Given um usuário autenticado abrindo a tela "Transações" ou
  "Categorias e orçamentos"
- When o papel dele ainda está sendo determinado (chamadas em
  andamento)
- Then nenhum botão de escrita (criar/editar/excluir) é exibido até o
  papel ser conhecido

## Contratos consumidos (já implementados no backend, sem mudança)

Nenhum contrato novo — endpoints e respostas já existem e não mudam de
formato. Referência completa:
`backend/specs/FEAT-20-membros-convites-permissoes/spec.md` e
`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`.

### Resposta 403 (reaproveitada em `/transactions` e `/categories`)

```json
{
  "type": "https://gastosapp.dev/errors/insufficient-permission",
  "title": "Acesso negado",
  "status": 403,
  "detail": "Seu nível de acesso não permite esta ação."
}
```

### GET /members e GET /auth/me

Mesmos contratos já consumidos pela FEAT-28
(`frontend/specs/FEAT-28-membros-convites/spec.md`) — usados para
determinar o papel do usuário logado na conta ativa (comparação por
e-mail entre o item de `GET /members` e `GET /auth/me`).

## Critérios de aceite

- [x] Papel `Leitura` não vê nenhum botão/ícone de escrita em
      Transações (criar despesa/receita, editar/excluir) nem em
      Categorias (criar, editar, excluir, orçamento)
- [x] Papel `Lancar` vê "+ Nova despesa"/"+ Nova receita", vê "Editar"/
      "Excluir" só nas transações que ele mesmo criou, e não vê nenhum
      botão de escrita em Categorias
- [x] Papéis `Total` e `Titular` têm acesso irrestrito a toda ação de
      escrita em Transações (qualquer transação, independente de quem
      criou) e Categorias (incluindo orçamento)
- [x] Um 403 defensivo em `/transactions` ou `/categories` mostra uma
      mensagem específica de acesso negado, nunca o erro genérico atual
- [x] Nenhum botão de escrita aparece enquanto o papel do usuário ainda
      não foi determinado
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

Implementado conforme `plan.md`/`tasks.md` (suíte completa: 517/517
testes passando). A revisão manual no app real (Docker local + contas
nos 4 papéis, `tasks.md` item 15) foi conscientemente adiada — a
cobertura automatizada já exercita cada cenário por papel nas duas
telas; retomar quando conveniente.

## Fora do escopo

- Qualquer mudança no contrato do backend — a matriz de autorização já
  está em produção desde a FEAT-20/FEAT-22
- Tela de Membros — já resolvida pela FEAT-28 (escrita restrita ao
  Titular, decisão 1 daquela spec); esta feature não altera nada lá
- Telas sem nenhuma ação de escrita hoje (Início/Dashboard, Relatórios)
  — nada muda nelas
- Bloquear acesso direto por URL a um formulário de criar/editar além
  de esconder o botão de entrada — mesma decisão já tomada na FEAT-28
  para Membros (proteção é só na superfície de navegação normal)
- Seletor/troca entre múltiplas contas ou exibição de mais de um papel
  ativo ao mesmo tempo — usuário só tem uma conta ativa por vez
  (decisão já fechada nas FEAT-19/FEAT-20 do backend)
- Qualquer papel novo além dos 4 já existentes (`Leitura`, `Lancar`,
  `Total`, `Titular`)
