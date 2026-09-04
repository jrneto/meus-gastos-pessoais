# Seed de usuários (local)

Popula um cenário de usuários fictícios — titular + convidados, categorias
de receita e lançamentos de receita/despesa — no ambiente **local** do
backend, chamando a API HTTP real (mesmos endpoints que o frontend usa).
Nunca aponta pra homologação/produção. Não é uma feature do produto — é
uma ferramenta de apoio pra testar manualmente o app com dados realistas.

## Pré-requisitos

```bash
cd backend/infra
docker compose up -d          # LocalStack + cognito-local
./scripts/local-init.sh       # uma vez, cria o User Pool local
cd ../src/GastosApp.Api && dotnet run   # porta 5049
```

## Uso

```bash
cd backend/infra
./scripts/seed-users/seed-scenario.sh cenario1
```

Sem argumento, lista os cenários disponíveis (`scripts/seed-users/scenarios/*.sh`).

## Criando um novo cenário

Copie `scenarios/cenario1.sh` e ajuste os dados — é só um arquivo de
variáveis/arrays bash, sem lógica (a lógica toda vive em
`seed-scenario.sh`, reaproveitada por qualquer cenário):

- `TITULAR_EMAIL` / `TITULAR_CPF` / `TITULAR_TELEFONE` / `TITULAR_NOME`
- `CONVIDADOS`: array `email|cpf|telefone|nome|role`, `role` ∈
  `Leitura`/`Lancar`/`Total`
- `CATEGORIAS_RECEITA`: array `nome|valor_mensal_em_reais`
- `SENHA_PADRAO`: senha única usada por todos os usuários do cenário

## O que o script cria

1. **Titular** — `POST /auth/register`, confirmado via
   `aws cognito-idp admin-confirm-sign-up` contra o cognito-local (sem
   depender de e-mail real, mesmo padrão de
   `GastosApp.IntegrationTests/Support/TestAccountFixture.cs`), depois
   `POST /auth/login`.
2. **Convidados** — um `POST /members` (convite) por convidado, seguido do
   mesmo registro/confirmação/login acima. O primeiro login de cada
   convidado aceita o convite automaticamente e passa a operar na conta do
   titular (FEAT-20).
3. **Categorias de receita** — uma `POST /categories` por item de
   `CATEGORIAS_RECEITA`, com o titular.
4. **Lançamentos de receita** — um por categoria de receita, todo mês
   (dia 05, ou hoje se ainda não chegou no dia 05 do mês corrente), desde
   o dia 01 do mês há 3 meses até hoje (4 meses).
5. **Lançamentos de despesa** — 1 por categoria padrão (as 13 despesas que
   toda conta já nasce com, ver `backend/specs/FEAT-28-seed-categorias-padrao/`),
   por dia, desde o dia 01 do mês há 3 meses até hoje — gerado pelo titular
   **e** por cada convidado com permissão de lançar (`Lancar`/`Total`;
   `Leitura` fica de fora, não tem permissão de `POST /transactions`).

Valores e descrições de despesa são fictícios/aleatórios (só pra povoar
dado, sem significado).

## Gotchas

- **Curl + acento no Windows**: `curl.exe` recodifica argumentos de linha
  de comando pro codepage ativo antes do processo receber — um payload
  JSON passado direto em `-d '...'` com "Educação"/"Vale alimentação"
  chega corrompido no servidor (`500 Cannot transcode invalid UTF-8 JSON
  text`), mesmo com o bash mostrando os bytes certos internamente. Passar
  o payload por arquivo (`--data-binary @arquivo`) não resolve sozinho
  porque `MSYS_NO_PATHCONV=1` (necessário pro AWS CLI não corromper
  `/GastosApp/...`) também desliga a conversão de path que o curl.exe
  precisa pra achar o arquivo. O script usa `--data-binary @-` lendo de
  stdin (`<<< "$data"`) — não passa acento pelo argv nem precisa resolver
  path nenhum. Nunca trocar por `-d "$json"` nem `@arquivo` enquanto o
  payload puder ter acento.
- **Lançamentos não são idempotentes**: não há chave natural pra
  deduplicar receita/despesa na API, então rodar o mesmo cenário duas
  vezes duplica tudo. O script detecta se a conta do titular já tem
  lançamento e pede confirmação antes de continuar. Pra recomeçar do
  zero: `./scripts/reset-dynamodb.sh && ./scripts/reset-cognito.sh`.
- **Volume**: com 3 meses de histórico, o cenário de exemplo (13
  categorias × ~90 dias × 3 usuários com permissão de lançar, mais as
  receitas) gera perto de 3700 requisições — o script roda em alguns
  minutos, não trave se parecer parado, ele imprime progresso por dia.
- Usuários/convites/categorias **são** idempotentes — pode rodar de novo
  sobre o mesmo ambiente sem duplicar (a API responde 409/422 e o script
  reaproveita o que já existe).
