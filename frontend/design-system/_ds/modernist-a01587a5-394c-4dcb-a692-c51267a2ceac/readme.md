# Design system Modernist

Modernist é plano, arquitetônico e todo composto em Archivo: um vermelho quase monocromático sobre branco, uma grade modular visível, raio de canto zero e regras fortes de 2px. Nada flutua e nada é decorado — o alinhamento e a força das divisórias fazem toda a organização, os rótulos ficam alinhados à esquerda (mesmo dentro de botões), e a fotografia é impressa em preto e branco puro.

## Como usar isto

- Vincule a única stylesheet em cada página — `<link rel="stylesheet" href="styles.css">` (ajuste o caminho relativo) — e use todas as cores, fontes, espaçamentos, raios e sombras a partir de suas variáveis (`var(--color-*)`, `var(--font-*)`, `var(--space-*)`, `var(--radius-*)`, `var(--shadow-*)`). Nunca fixe um hex, um nome de fonte ou um valor em px que os tokens já carregam.
- Construa com as classes abaixo em vez de inventar classes paralelas; as páginas de componentes são HTML puro, então veja o código-fonte e copie a marcação.
- `templates/` contém pontos de partida que um projeto consumidor pode copiar inteiramente.
- Todo o sistema foi derivado de `theme.json`. Para mudar a aparência, edite os tokens no topo de `styles.css` — cada página, a miniatura e este guia leem a partir deles — e mantenha `theme.json` e a documentação escrita sincronizados para não se distanciarem do que o CSS realmente faz.

## Direção

Layouts em grade modular — conteúdo em células de largura igual, ritmo horizontal e vertical forte, estrutura visível. Use divisórias fortes de 2px (`var(--color-divider)`) entre seções principais. Os rótulos dos botões ficam alinhados à esquerda — um botão mais largo que seu rótulo começa o texto na borda esquerda do preenchimento (incluindo ícone à direita), nunca centralizado. Envolva imagens de hero e inline na classe `.grayscale` — elas são impressas em preto e branco puro.

## Cor

Um fundo claro (`--color-bg` #f3f2f2) com `--color-text` #201e1d e um único acento #ec3013 (este é um esquema monocromático: nenhum segundo acento foi escolhido — as variáveis `--color-accent-2-*` carregam um substituto gerado por máquina, mantido apenas para que ambos os conjuntos resolvam; trate-as como um único papel). Cada papel carrega uma rampa tonal de 100–900 (`--color-neutral-100` … `--color-accent-2-900`) gerada em OKLCH numa escala de luminosidade perceptual compartilhada, de forma que o mesmo degrau de qualquer rampa tenha o mesmo peso visual. Use os degraus claros (100–300) para preenchimentos tintados, hovers e bordas sutis, 500 como base do papel, e os degraus escuros (700–900) para texto sobre preenchimentos tintados e para estados pressionados; prefira os degraus da rampa a `color-mix()` improvisado. Para elevação, use `--shadow-sm/md/lg` (já ajustadas ao fundo) em vez de box-shadows improvisadas.

## Tipografia

Archivo para títulos sobre Archivo para o corpo do texto, carregadas como `--font-heading` / `--font-body`. Densidade 1,00× e raio 0px já estão embutidos nas escalas `--space-*` / `--radius-*` — use as variáveis, não números brutos.

## Ícones

Use ícones Lucide (https://lucide.dev) em todo o sistema.

## Estados de interação

Os estados interativos são temáticos, nunca os padrões do navegador: dê a cada elemento interativo um tingimento de `:hover` e um estado pressionado a partir da rampa de acento (um degrau além da base — `--color-accent-600` sobre fundo claro, `--color-accent-400` sobre fundo escuro, ou um tingimento `color-mix()` para variantes outline/ghost), e estilize o foco de teclado com `:focus-visible { outline: 2px solid var(--color-accent); outline-offset: 2px; }` — nunca deixe o anel de foco azul padrão.

## Componentes

| Classe | O que é | Mostrado em |
| --- | --- | --- |
| `.btn` com `.btn-primary`, `.btn-secondary`, `.btn-ghost`, `.btn-icon`, `.btn-block` | Ações — o primário é um preenchimento sólido de acento | components/buttons.html |
| `.tag` com `.tag-accent`, `.tag-accent-2`, `.tag-neutral`, `.tag-outline` | Rótulos pequenos tingidos a partir das rampas (paleta monocromática: accent-2 lê igual ao accent) | components/buttons.html |
| `.field` + `label`, `.input`, `.radio` + `.dot`, `.seg` + `.seg-opt` | Campos de formulário e escolhas em elementos nativos — sem script | components/forms.html |
| `.card` com `.card-kicker`, `.card-title`, `.card-body`, `.card-meta`; `.elev-sm/md/lg` | Cards de conteúdo com preenchimento de superfície; utilitários de elevação | components/cards.html |
| `.nav` + `.nav-brand` | A barra de cabeçalho | components/navigation.html |
| `.table` | Tabelas de dados com cabeçalho temático e regras de linha | components/table.html |
| `.dialog-backdrop` + `.dialog` (+ `.dialog-title/-body/-actions`) | Um modal na elevação mais alta | components/dialog.html |
| `.hr` | Uma regra horizontal forte de 2px | foundations/layout.html |
| `.grayscale` | O invólucro de imagem — toda fotografia de conteúdo passa por ele | foundations/image.html |

Os estados já vêm prontos: hovers e estados pressionados vêm da rampa de acento, o foco de teclado é o anel `:focus-visible` de 2px em acento, `::selection` é um tingimento de acento, e controles desabilitados caem para 45% de opacidade. Não reestilize por página. O par acento-fundo é ajustado para pelo menos 3:1 — suficiente para ícones, texto grande e elementos de interface, mas não para texto corrido — então para texto de parágrafo em acento use um degrau profundo da rampa (`--color-accent-700` sobre este fundo) em vez do acento puro.

## Faça

- Deixe a grade aparecer: células de largura igual, regras horizontais fortes entre seções, estrutura visível.
- Mantenha tudo alinhado à esquerda — títulos, texto e os rótulos dentro de botões largos.
- Use o acento com moderação, para a ação primária e pequenas ênfases; o sistema é majoritariamente tinta sobre fundo. O único lugar em que o vermelho ocupa um campo inteiro é a declaração-pôster — as divisórias de seção do deck e o banner de fechamento da landing — onde a tipografia permanece em nível display e o acento carrega a página.
- Imprima fotografias em preto e branco com o invólucro `.grayscale`.

## Não faça

- Não arredonde nenhum canto — `--radius-md` é 0 de propósito.
- Não centralize rótulos de botão ou texto de hero.
- Não suavize as regras em hairlines nem as troque por espaço em branco.
- Não tinja ou colorize imagens.

## Arquivos

- `styles.css` — a única stylesheet: a folha de tokens (variáveis `:root`, rampas, tipografia base) mais a camada de componentes. Vincule-a em cada página.
- `readme.md` — este guia.
- `theme.json` — os parâmetros dos quais estes arquivos foram derivados (um registro legível por máquina do tema).
- `thumbnail.html` — a capa do projeto (marca + amostras de cor).
- `foundations/type.html` — a escala tipográfica e o par título/corpo em tamanhos reais.
- `foundations/color.html` — papéis de cor e as rampas tonais 100-900, com notas de uso.
- `foundations/layout.html` — a escala de espaçamento, a grade e como as bordas são desenhadas.
- `foundations/icons.html` — o conjunto de ícones em tamanhos de interface, inline e em botões.
- `foundations/image.html` — como fotografias e figuras são tratadas.
- `components/buttons.html` — botões, botões de ícone e tags em cada variante e estado.
- `components/forms.html` — campos de texto, radios e o controle segmentado em elementos nativos.
- `components/cards.html` — cards de conteúdo e os degraus de elevação.
- `components/navigation.html` — o padrão da barra de cabeçalho.
- `components/table.html` — uma tabela de dados com o cabeçalho temático e regras de linha.
- `components/dialog.html` — um modal sobre seu backdrop na elevação mais alta.
- `theme.html` — os parâmetros do tema renderizados como folha de referência.
- `templates/landing/` — uma página inicial que consome o sistema da forma pretendida (`index.html`, seu loader `ds-base.js` e o `image-slot.js` vendorizado que monta sua fotografia).
- `assets/photo.jpg` — a fotografia de referência tratada na página de imagem.
