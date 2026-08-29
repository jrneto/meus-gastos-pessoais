# Backlog — Frontend

Registro de débitos técnicos e oportunidades de melhoria identificados
durante o trabalho no frontend (specify/plan/tasks/implementação/review
ou Modo Leve), fora do escopo do que estava sendo feito no momento. Ver
"Débitos técnicos e oportunidades de melhoria" no
[`/CLAUDE.md`](../../CLAUDE.md) raiz do monorepo para o processo de
como um item chega aqui.

**Como usar este arquivo:** ao decidir priorizar um item, ele vira
`spec.md` própria em `frontend/specs/{FEAT-XX-nome}/` (Fluxo Completo)
ou é resolvido direto (Modo Leve), conforme "Modo Leve vs Fluxo
Completo" no `/CLAUDE.md` raiz. Depois de implementado, remover o item
deste arquivo.

## Débitos técnicos e melhorias futuras

- **Componente de toast genérico (Modernist)** — levantado durante o
  `/plan` da FEAT-21 (cadastro real). O design já assume toasts de
  confirmação em pelo menos 3 telas
  (`design-system/web/screenshots/09-toast-despesa-lancada.png`,
  `17-toast-convite-enviado.png`, e o próprio fluxo de cadastro), mas
  nenhum componente de toast existe hoje no código — cada tela usa o
  padrão inline de sucesso/erro (`<p role="alert">`). A FEAT-21 seguiu
  com o padrão inline por decisão do usuário, para não aumentar escopo.
  Relevante para FEAT-24 (nova receita) e FEAT-28 (membros/convite) do
  backlog abaixo, que também assumem toast no design — vale resolver
  uma vez, de forma genérica, antes delas.
- **Overlay de processamento de tela cheia (Modernist)** — levantado no
  mesmo `/plan` da FEAT-21. O design mostra um véu sobre o fundo com
  spinner e barra de progresso indeterminada para ações mais longas
  (`04-login-processando.png`, `08-salvando-despesa-loading.png`,
  `16-enviando-convite-loading.png`), também inexistente no código hoje
  — só o padrão de botão ocupado (spinner + label em gerúndio +
  `disabled`) é usado. Mesma relevância futura que o item acima.

