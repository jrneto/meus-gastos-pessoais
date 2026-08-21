# FEAT-14: Migração para o design system Modernist — Login

## Objetivo

Trocar a linguagem visual do frontend pelo novo design system **Modernist**
(gerado em `frontend/design-system/`), começando exclusivamente pela tela
de **Login**. Esta feature serve como piloto: valida o novo sistema de
tokens/componentes em produção antes de estender a migração ao restante
do app (dashboard, despesas, categorias etc.), que ficam fora do escopo
aqui e serão cobertos por specs futuras.

## Contexto

O usuário criou, no Claude Design, um design system próprio chamado
**Modernist** — plano, monocromático (vermelho `#ec3013` sobre branco),
tipografia Archivo, raio de canto zero, regras de 2px — junto com um
protótipo clicável (`design_handoff_jrnexpenses_prototype/`) e uma versão
web (`jrnexpenses-web.dc.html`) mostrando como as telas do jrnexpenses se
comportariam nesse sistema. Os artefatos em `frontend/design-system/` são
**referência de design** (HTML de protótipo + a stylesheet `_ds/.../
styles.css` com os tokens/classes reais), não código de produção — a
tarefa é recriar a linguagem visual nos componentes React existentes.

Hoje `frontend/docs/constitution.md` documenta a stack de UI como
**shadcn/ui + Tailwind CSS**. Esta feature não substitui essa stack de
uma vez: introduz o Modernist **convivendo lado a lado** com shadcn/ui +
Tailwind, isolado à tela de Login. `constitution.md` é atualizado para
registrar essa transição em andamento (stack de UI em migração,
tela por tela) e apontar `frontend/design-system/` como a fonte dos
tokens. Novas specs futuras (fora deste escopo) migram o restante do app
e, quando toda a superfície estiver migrada, removem shadcn/ui/Tailwind
e os componentes hoje em `components/ui/`.

Não há mudança de contrato com o backend nem de regra de negócio para o
fluxo de **login**: o formulário continua chamando o mesmo endpoint
(`backend/specs/FEAT-01-auth`), com as mesmas regras de validação (Zod)
e o mesmo tratamento de erro (`InvalidCredentialsError`, etc.) já
implementados em `features/auth/`. O que muda nele é exclusivamente a
camada visual: marcação, classes CSS e tokens usados por `LoginPage` e
`LoginForm`.

O design (`jrnexpenses-web.dc.html`) mostra um controle segmentado
"Entrar / Criar conta" na tela de autenticação. O backend hoje só expõe
login (não há endpoint de cadastro), então **o modo "Criar conta" é
recriado apenas na camada visual**, sem integração real: o segmentado
alterna para o layout de cadastro (campo Nome adicional, rótulo do botão
"Criar conta"), mas submeter esse formulário não chama nenhuma API —
apenas navega para uma página estática de placeholder ("em breve"/fake),
deixando claro que o cadastro real ainda não existe. Cadastro de verdade
(endpoint + integração) fica para uma spec própria futura.

## Requisitos de negócio

- Nenhuma regra de validação ou de negócio do **login** muda nesta
  feature: e-mail e senha continuam obrigatórios, senha mínima de 8
  caracteres (Zod, já implementado em
  `features/auth/schemas/loginSchema.ts`), mensagens de erro de
  credencial inválida e de erro de rede seguem como hoje
- A tela de Login (`LoginPage` + `LoginForm`) passa a usar os tokens e
  classes do design system Modernist (`frontend/design-system/_ds/
  modernist-a01587a5-394c-4dcb-a692-c51267a2ceac/styles.css`) em vez das
  classes utilitárias Tailwind/shadcn atuais:
  - Cores, tipografia (Archivo, `--font-heading`/`--font-body`),
    espaçamento e raio (`0`) vêm exclusivamente das variáveis CSS do
    Modernist — nenhum hex, px ou nome de fonte hardcoded
  - Campos de formulário usam `.field` + `.input` do Modernist no lugar
    de `Input`/`Label` do shadcn/ui
  - Botão de submit usa `.btn .btn-primary .btn-block`, com o rótulo
    alinhado à esquerda (convenção do sistema), no lugar do `Button` do
    shadcn/ui
  - Mensagem de erro de credencial/rede usa a paleta de acento do
    Modernist (`--color-accent-700` para texto sobre fundo, conforme o
    guia do sistema) no lugar do componente `Alert` do shadcn/ui
  - Estados de foco de teclado usam o anel `:focus-visible` em acento do
    Modernist (nunca o anel azul padrão do navegador)
- Wordmark "jrn." (ponto final em `--color-accent`) + subtítulo em
  versalete "expenses" substituem o título genérico "Entrar" hoje
  renderizado em `LoginPage`
- Controle segmentado "Entrar / Criar conta" (`.seg`/`.seg-opt`) acima do
  formulário, como no design:
  - Modo "Entrar" (padrão): campos E-mail e Senha, botão "Entrar" — é o
    fluxo real, integrado ao backend, sem mudança de comportamento
  - Modo "Criar conta": campos Nome, E-mail e Senha, botão "Criar conta"
    — puramente visual; submeter **não** chama nenhuma API e navega para
    uma rota fake de placeholder (ex.: `/cadastro-em-breve`), sinalizando
    que o cadastro ainda não está implementado
  - Alternar entre os dois modos não deve disparar chamada de rede nem
    perder o usuário em nenhum estado de erro
- O restante do app (rotas fora de `/login`) continua **inalterado**,
  renderizado com shadcn/ui + Tailwind como hoje — nenhum componente
  compartilhado em `components/ui/` é removido ou alterado nesta feature
- `frontend/design-system/` passa a ser referenciado a partir de
  `frontend/docs/constitution.md` como a fonte dos tokens visuais em uso
  (documentando a stack de UI como "em transição: Modernist na tela de
  Login, shadcn/ui + Tailwind no restante")
- Sem mudança de contrato de API, sem novo endpoint real de cadastro,
  sem novo recurso AWS

## User stories

### Visitante não autenticado acessa `/login`

- **Given** um visitante não autenticado navega para `/login`
- **When** a página carrega
- **Then** vê a wordmark "jrn." (ponto final em vermelho de acento), o
  subtítulo "expenses", o controle segmentado "Entrar / Criar conta" (com
  "Entrar" ativo por padrão) e o formulário de login (e-mail, senha,
  botão "Entrar"), tudo com a linguagem visual do design system
  Modernist (tipografia Archivo, cantos retos, campos e botão nos
  estilos `.input`/`.btn`)

### Login com credenciais válidas

- **Given** o visitante preenche e-mail e senha válidos na tela migrada
- **When** submete o formulário no modo "Entrar"
- **Then** o comportamento é idêntico ao atual: sessão autenticada é
  criada e o visitante é redirecionado para a rota protegida inicial

### Login com credenciais inválidas

- **Given** o visitante submete e-mail/senha incorretos no modo "Entrar"
- **When** a API responde com erro de credencial inválida
- **Then** a mensagem de erro aparece estilizada com os tokens do
  Modernist (texto em `--color-accent-700`), mantendo o mesmo texto/
  comportamento de hoje (nenhum redirecionamento, campos preservados)

### Validação client-side (modo Entrar)

- **Given** o visitante submete o formulário com e-mail em formato
  inválido ou senha abaixo de 8 caracteres
- **When** o Zod valida os campos
- **Then** os erros inline aparecem nos campos correspondentes, com a
  tipografia/cor de erro do Modernist, sem chamar a API

### Alternar para o modo "Criar conta"

- **Given** o visitante está na tela de Login
- **When** seleciona "Criar conta" no controle segmentado
- **Then** o formulário passa a exibir o campo Nome além de E-mail e
  Senha, e o botão muda o rótulo para "Criar conta"; nenhuma chamada de
  rede ocorre nessa troca

### Submeter o modo "Criar conta" (fluxo fake)

- **Given** o visitante preencheu o formulário no modo "Criar conta"
- **When** submete
- **Then** nenhuma chamada à API de autenticação é feita; o visitante é
  navegado para uma página estática de placeholder informando que o
  cadastro ainda não está disponível

## Fora do escopo

- Migrar qualquer outra tela (dashboard, despesas, categorias, ajustes)
  para o Modernist — feito em specs futuras, uma tela por vez
- Implementar cadastro de usuário de verdade (endpoint de signup no
  backend + integração real no frontend) — o modo "Criar conta" desta
  feature é só a casca visual + página fake de destino
- Remover shadcn/ui, Tailwind ou os componentes em `components/ui/` do
  projeto — só ocorre quando todas as telas estiverem migradas
- Qualquer alteração em `backend/` (endpoints, contrato, regras de login)
- Link "Esqueceu a senha?" (aparece no protótipo mobile, ausente na
  versão web usada como referência aqui) — fora de escopo até existir
  fluxo de recuperação de senha no backend
- Provisionamento ou alteração de infraestrutura AWS

## Critérios de aceite

- [x] `LoginPage`/`LoginForm` renderizam usando classes e tokens do
      design system Modernist, sem nenhuma classe Tailwind/shadcn
      remanescente nesses componentes
- [x] Wordmark "jrn." + subtítulo "expenses" substituem o título "Entrar"
      atual
- [x] Controle segmentado "Entrar / Criar conta" funcional (troca de modo
      sem chamada de rede)
- [x] Login com sucesso, erro de credencial inválida, erro de rede e
      validação client-side (modo Entrar) continuam funcionando
      exatamente como hoje (mesmas classes de erro tipadas, mesmo
      redirecionamento)
- [x] Submissão no modo "Criar conta" não chama a API de login e navega
      para uma página fake de placeholder
- [x] Nenhuma outra tela do app é alterada visualmente
- [x] `frontend/docs/constitution.md` atualizado descrevendo a stack de
      UI em transição e referenciando `frontend/design-system/` como
      fonte dos tokens do Modernist
- [x] 100% dos testes (unitários/componente) de `features/auth/` e
      `routes/LoginPage` passando após a migração
