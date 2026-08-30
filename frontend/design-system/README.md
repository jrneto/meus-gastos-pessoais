# jrnexpenses — Export completo (web + mobile)

Protótipos funcionais do jrnexpenses nas duas plataformas, construídos sobre o design system **Modernist** (near-mono vermelho #ec3013 sobre fundo claro, Archivo, zero raio, réguas de 2px).

```
web/      jrnexpenses-web.dc.html  + dependências + 19 screenshots
mobile/   jrnexpenses.dc.html      + dependências + 16 screenshots
```

Cada pasta é autossuficiente (inclui `_ds/modernist-…/` com `styles.css`, tokens e bundle). Abra o `.dc.html` por um servidor local — ex. `python3 -m http.server` na raiz da pasta — para que os arquivos relativos carreguem.

## Web — telas (`web/screenshots/`)

01 login · 02 criar conta · 03 criar conta preenchida (nome, CPF, telefone, e-mail, senha) · 04 login processando · 05 dashboard · 06–07 nova despesa · 08 salvando (loading) · 09 toast de confirmação · 10 nova receita · 11 transações · 12 relatórios · 13 categorias e orçamentos · 14 membros · 15 convidar pessoa · 16 enviando convite (loading) · 17 toast do convite · 18 ajustes · 19 detalhe de transação

## Mobile — telas (`mobile/screenshots/`)

01 onboarding · 02 login · 03 criar conta · 04 criar conta preenchida · 05 dashboard · 06–07 nova despesa · 08 dashboard após salvar · 09 nova receita · 10 transações · 11 relatórios · 12 ajustes · 13 categorias e orçamentos · 14 membros e convites · 15 membro convidado · 16 detalhe de transação

## Cadastro de conta

Modo "Criar conta" pede **nome completo, CPF, telefone, e-mail e senha**. CPF e telefone têm máscara progressiva no próprio campo (`000.000.000-00` e `(11) 98765-4321`), `inputmode` numérico e limite de dígitos. No web os dois ficam lado a lado em grid de 2 colunas; no mobile, empilhados. O modo "Entrar" continua com apenas e-mail e senha.

## Estados de processamento

Três camadas, todas com tokens do Modernist:

1. **Botão ocupado** — spinner dentro do botão, rótulo em gerúndio ("Entrando…", "Salvando…"), botão desabilitado.
2. **Overlay de processamento** — véu do fundo a ~88%, spinner, rótulo em caixa alta e barra de progresso indeterminada; Cancelar desabilitado.
3. **Toast de confirmação** — fundo tinta sobre texto claro, mensagem que informa o efeito ("Despesa lançada e orçamento atualizado."), desaparece em 3,2s.

Latência simulada de 1,7–1,8s. Em produção, trocar os `setTimeout` por promessas da API mantendo exatamente os mesmos estados.

## Modelo de dados do protótipo

Receitas e despesas vivem na mesma lista (`transactions`) com campo `type: 'income' | 'expense'`. Cada categoria também carrega `type` — a tela de Categorias agrupa em **Categorias de despesa** (com teto mensal e barra de consumo, tile e etiqueta em vermelho de acento) e **Categorias de receita** (com valor previsto e realizado em verde, sem barra). Não há mais distinção de despesa fixa × variável. O dashboard calcula saldo do mês = receitas − despesas.

Categorias de receita não têm teto nem valor previsto — mostram apenas o realizado do mês. O campo de anexar comprovante foi removido de despesa, receita e do detalhe do lançamento.

> Nota: alguns screenshots ainda refletem versões anteriores dessas telas; os arquivos `.dc.html` estão sempre na versão atual.
