# FEAT-35: Membros inativos (aderência à FEAT-41 do backend)

## Objetivo

Adequar a tela "Membros da conta" ao novo comportamento do backend
(`backend/specs/FEAT-41-inativacao-membros-com-transacoes/spec.md`):
`DELETE /members/{id}` deixou de sempre apagar o vínculo de fato — um
membro `Ativo` com pelo menos uma transação lançada passa a
`Status=Inativo` em vez de ser removido, para preservar o histórico
(`createdByLabel` das transações continua mostrando o e-mail dele em
vez de "Ex-membro"). Hoje o frontend não sabe da existência desse
status: assume que toda remoção bem-sucedida apaga o membro da lista, o
tipo de `status` não contempla `"Inativo"`, e as ações de trocar papel/
remover continuam disponíveis para um membro já inativo — o que hoje
resulta nos dois novos erros 422 do backend caindo no fallback
genérico ("Ocorreu um erro inesperado").

## Contexto

A API (`backend/docs/openapi.json`) já reflete o contrato novo em
produção/homologação — nenhuma mudança de formato de request/response,
só um valor novo de `status` (`"Inativo"`) em `GET /members` e dois
novos casos de erro 422 em `PUT`/`DELETE /members/{id}`
(`cannot-modify-inactive-member`, `member-already-inactive`). O
problema é inteiramente do lado do consumo: o frontend (`features/
members/`) precisa passar a tratar esse status e esses erros.

Ponto central desta feature: **a resposta de `DELETE /members/{id}` é
idêntica nos dois casos** (204, sem corpo) — remoção de fato ou
inativação não são distinguíveis pela resposta em si (ver
`backend/specs/FEAT-41.../spec.md`, seção "Contratos da API" ›
`DELETE /members/{id}`). Hoje a tela remove o item da lista local
assim que a chamada retorna com sucesso (`MembersPage.handleRemoved`),
o que está errado quando o resultado real foi uma inativação: o membro
deveria continuar visível, marcado `Inativo`, não desaparecer.

## Requisitos de negócio

- `status` de um membro pode ser `"ConvitePendente"`, `"Ativo"` ou
  `"Inativo"` — o tipo usado no frontend precisa contemplar os três.
- Um membro `Inativo` é sempre exibido na lista (nunca escondido, nunca
  filtrado), com um rótulo que o distingue claramente de `Ativo` e de
  `ConvitePendente`.
- Um membro `Inativo` nunca tem ação de trocar papel nem de remover
  disponível na interface, independente de quem está vendo a tela —
  essas ações não têm efeito útil sobre ele (a API responde 422 pros
  dois casos) e não devem ser oferecidas.
- Como a resposta de `DELETE /members/{id}` não diferencia remoção de
  fato de inativação, a tela não pode decidir localmente (por
  otimismo) se o membro deve sumir ou continuar aparecendo — precisa
  reconciliar com o estado real devolvido pelo backend após a chamada
  (nova consulta da lista), não simplesmente remover o item do estado
  local como acontece hoje.
- Remoção de um `ConvitePendente` ou de um `Ativo` sem transações
  continua resultando no membro sumindo da lista (comportamento atual,
  sem regressão) — a diferença é *como* a tela chega a essa conclusão
  (reconciliando com o servidor, não assumindo).
- Os dois novos erros 422 (`cannot-modify-inactive-member`,
  `member-already-inactive`) são mapeados para mensagens específicas,
  não para o erro genérico atual.
- O texto de confirmação de remoção não afirma que a ação "não pode ser
  desfeita" nem promete apagamento definitivo — o efeito garantido e
  imediato, em qualquer um dos dois casos, é a perda de acesso à conta;
  o texto deve refletir isso sem prometer um resultado que pode não
  corresponder ao que de fato acontece no backend.
- Reconvidar o e-mail de um membro `Inativo` (`POST /members`) não
  precisa de nenhum tratamento especial no frontend — a checagem de
  duplicidade do backend já ignora `Inativo` (FEAT-41); o fluxo de
  convite existente deve continuar funcionando sem alteração para esse
  caso.

## User Stories

**US1 — Lista mostra o status "Inativo" claramente**
- Given `GET /members` retorna um membro com `status: "Inativo"`
- When a tela "Membros da conta" é carregada
- Then esse membro aparece na lista com um rótulo que o identifica como
  inativo (nunca como "Ativo" nem em branco)

**US2 — Nenhuma ação de escrita é oferecida para um membro inativo**
- Given a tela lista um membro com `status: "Inativo"`
- When o Titular visualiza essa linha
- Then não há seletor de papel nem botão de remover para esse membro
  (mesmo padrão hoje aplicado à linha do Titular, que também não expõe
  essas ações)

**US3 — Remover membro com transações mantém ele visível como Inativo**
- Given o Titular clica em "Remover" para um membro `Ativo` que já
  lançou alguma transação e confirma
- When a API responde 204 e o backend, de fato, inativou o membro em
  vez de apagá-lo
- Then a tela reflete isso: o membro continua aparecendo na lista,
  agora com `status: "Inativo"`, em vez de desaparecer

**US4 — Remover convite pendente ou membro sem transações continua
removendo de fato**
- Given o Titular remove um `ConvitePendente` ou um `Ativo` sem nenhuma
  transação lançada
- When a API responde 204 e o backend removeu o vínculo de fato
- Then o membro desaparece da lista (sem regressão do comportamento
  atual)

**US5 — Erro ao trocar papel de um membro que virou inativo entretanto**
- Given um membro tinha `status: "Ativo"` quando a tela carregou, mas
  virou `Inativo` por uma ação concorrente (outra aba, outro
  dispositivo) antes do Titular trocar o papel dele
- When o Titular tenta trocar o papel e a API responde 422
  (`cannot-modify-inactive-member`)
- Then a tela mostra uma mensagem específica sobre o motivo (não o erro
  genérico) e desfaz a mudança otimista de papel

**US6 — Erro ao tentar remover um membro que já está inativo**
- Given um membro tinha `status: "Ativo"` quando a tela carregou, mas
  virou `Inativo` por uma ação concorrente antes do Titular confirmar a
  remoção
- When o Titular confirma a remoção e a API responde 422
  (`member-already-inactive`)
- Then a tela mostra uma mensagem específica sobre o motivo (não o erro
  genérico)

**US7 — Confirmação de remoção não promete apagamento definitivo**
- Given o Titular abre o diálogo de confirmação de remoção para
  qualquer membro que não seja o Titular
- When o diálogo é exibido
- Then o texto comunica que o membro perderá acesso à conta
  imediatamente, sem afirmar que o registro será apagado de forma
  irreversível

**US8 — Reconvidar o e-mail de um membro inativo funciona normalmente**
- Given um e-mail pertence hoje a um membro `Inativo`
- When o Titular convida esse mesmo e-mail de novo via "Convidar
  pessoa"
- Then o convite é aceito (201) e aparece na lista como um novo
  `ConvitePendente`, coexistindo com o registro `Inativo` antigo — sem
  nenhum erro de "já é membro"

## Contrato consumido

Fonte de verdade: `backend/docs/openapi.json` e
`backend/specs/FEAT-41-inativacao-membros-com-transacoes/spec.md`. Sem
mudança de formato de request/response — só os itens abaixo, novos
para o frontend:

- `GET /members` → item da lista pode ter `status: "Inativo"`, além dos
  já conhecidos `"ConvitePendente"`/`"Ativo"`.
- `PUT /members/{id}` → novo caso de erro:
  ```json
  {
    "type": "https://gastosapp.dev/errors/cannot-modify-inactive-member",
    "title": "Regra de negócio violada",
    "status": 422,
    "detail": "Não é possível alterar o papel de um membro inativo."
  }
  ```
- `DELETE /members/{id}` → novo caso de erro:
  ```json
  {
    "type": "https://gastosapp.dev/errors/member-already-inactive",
    "title": "Regra de negócio violada",
    "status": 422,
    "detail": "Este membro já está inativo."
  }
  ```
- `DELETE /members/{id}` → resposta de sucesso (204, sem corpo) não
  diferencia remoção de fato de inativação — ver "Contexto" acima.

## Critérios de aceite

- [x] Tipo de `status` do membro no frontend contempla `"Inativo"`
- [x] Membro `Inativo` aparece na lista com rótulo próprio, distinto de
      `Ativo`/`ConvitePendente`
- [x] Membro `Inativo` não exibe seletor de papel nem botão de remover
- [x] Após uma remoção bem-sucedida (204), a tela reconcilia o estado
      real do membro com o backend em vez de assumir que ele
      desapareceu — membro inativado continua visível como `Inativo`,
      membro removido de fato desaparece
- [x] Erro 422 `cannot-modify-inactive-member` exibe mensagem
      específica e desfaz a troca otimista de papel
- [x] Erro 422 `member-already-inactive` exibe mensagem específica
- [x] Texto do diálogo de confirmação de remoção não promete
      apagamento irreversível — comunica perda de acesso imediata
- [x] Reconvidar o e-mail de um membro `Inativo` funciona sem erro e
      sem tratamento especial na UI
- [x] Remoção de `ConvitePendente`/`Ativo` sem transações continua
      removendo o membro da lista (sem regressão)
- [x] 100% dos testes (unitários/componente) passando

## Fora do escopo

- Reativar um membro `Inativo` — o backend não expõe essa ação (ver
  "Fora do escopo" de `backend/specs/FEAT-41.../spec.md`); a única
  forma de um e-mail voltar a ter acesso é um novo convite (US8)
- Filtrar a lista de membros por status
- Indicar antecipadamente (antes da tentativa de remoção) se um membro
  tem transações lançadas — não existe endpoint para isso; a tela só
  descobre o resultado depois de reconciliar com o backend
- Qualquer mudança em `/categories` ou `/transactions`
- Redesenho visual da tela "Membros da conta" além do necessário para
  acomodar o status novo (segue o padrão visual já existente de
  `MemberRow`/`MemberList`, tokens Modernist)
