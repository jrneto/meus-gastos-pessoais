# E-mails transacionais

Quatro arquivos HTML autocontidos, prontos para colar na ferramenta de disparo.
Tabelas aninhadas, estilos inline, sem imagens, sem JavaScript — renderizam em Gmail, Outlook e Apple Mail.

| Arquivo | Quando dispara | Assunto sugerido |
| --- | --- | --- |
| 01-confirmacao-cadastro.html | Ao criar a conta | Seu código de confirmação: {{codigo}} |
| 02-recuperacao-senha.html | Ao pedir "Esqueci minha senha" | Código para redefinir sua senha: {{codigo}} |
| 03-senha-alterada.html | Após a senha ser redefinida | Sua senha do jrn.expenses foi alterada |
| 04-boas-vindas.html | Após o e-mail ser confirmado | Sua conta está pronta, {{nome}} |

## Variáveis

{{nome}} · {{email}} · {{codigo}} · {{data}} · {{dispositivo}}

Substitua pela sintaxe da sua ferramenta se ela usar outro formato.
O código OTP expira em 1 minuto — o texto dos e-mails 01 e 02 declara isso; se você mudar o tempo no backend, ajuste a cópia.

## Antes de enviar

- Troque as URLs app.jrnexpenses.com.br pelos links reais.
- Confirme o endereço de suporte no rodapé.
- E-mails transacionais dispensam link de descadastro; o de boas-vindas traz "Gerenciar preferências".
