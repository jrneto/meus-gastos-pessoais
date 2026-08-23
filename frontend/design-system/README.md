# jrnexpenses — Protótipo Web (handoff)

Protótipo funcional da versão desktop/web do jrnexpenses, construído sobre o design system **Modernist**.

## Como abrir

Abra `jrnexpenses-web.dc.html` em um navegador (via servidor local, ex. `python3 -m http.server`, para que os arquivos relativos carreguem).

## Conteúdo

```
jrnexpenses-web.dc.html   protótipo completo (markup + lógica)
browser-window.jsx        moldura de janela de navegador
image-slot.js             placeholder de imagem (comprovantes)
support.js                runtime do componente
_ds/modernist-…/          design system: styles.css (tokens), bundle, readme
screenshots/              17 telas exportadas em PNG
```

## Telas exportadas

| Arquivo | Tela |
| --- | --- |
| 01-login | Login / criar conta |
| 02-login-loading | Autenticação em progresso (spinner no botão) |
| 03-dashboard | Resumo: saldo do mês, receitas, despesas, orçamentos |
| 04-nova-despesa | Diálogo de nova despesa |
| 05-nova-despesa-preenchida | Valor e categoria preenchidos |
| 06-loading-salvando-despesa | Estado de processamento: overlay + barra indeterminada |
| 07-toast-despesa-lancada | Confirmação por toast após salvar |
| 08-nova-receita | Diálogo de nova receita (categorias de receita) |
| 09-transacoes | Lista unificada, receitas em verde / despesas em vermelho |
| 10-relatorios | Relatórios por categoria e período |
| 11-categorias-orcamentos | Categorias e edição de orçamento |
| 12-membros | Membros e níveis de acesso |
| 13-convidar-pessoa | Diálogo de convite |
| 14-loading-enviando-convite | Envio de convite em progresso |
| 15-toast-convite-enviado | Confirmação do convite |
| 16-ajustes | Preferências e notificações |
| 17-detalhe-transacao | Detalhe de lançamento com comprovante |

## Padrão de estados de carregamento

Três camadas, todas com tokens do Modernist (sem cantos arredondados, acento #ec3013):

1. **Botão ocupado** — spinner de 15px dentro do botão, rótulo muda para gerúndio (“Entrando…”, “Salvando…”), botão desabilitado.
2. **Overlay de processamento** — cobre o diálogo com véu do fundo a 86%, spinner de 34px, rótulo em caixa alta e barra de progresso indeterminada. Cancelar fica desabilitado para evitar estado inconsistente.
3. **Toast de confirmação** — canto inferior direito, fundo tinta sobre texto claro, mensagem que informa o efeito (“Despesa lançada e orçamento atualizado.”), some em 3,2s.

Latência simulada: 1,7–1,8s. Em produção, substituir os `setTimeout` por promessas da API e manter os mesmos estados.
