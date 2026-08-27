# Tasks — FEAT-29: Testes integrados multiambiente + gate de CI/CD

`GastosApp.IntegrationTests` já está incluído em `GastosApp.sln`
(esqueleto desde a FEAT-03) — não precisa ser adicionado, só
reformulado.

## Fase 1 — Reformulação do projeto `GastosApp.IntegrationTests`

- [x] 1. Remover `<ProjectReference>` para `GastosApp.Api.csproj` e o
      `UnitTest1.cs` vazio de `GastosApp.IntegrationTests.csproj`;
      adicionar pacotes `AWSSDK.CognitoIdentityProvider`,
      `AWSSDK.DynamoDBv2` e `FluentAssertions` (mesma versão 8.10.0 já
      usada em `GastosApp.ComponentTests`)
- [x] 2. Criar `Support/IntegrationTestEnvironment.cs` — lê
      `INTEGRATION_TESTS_MODE` (`local`\|`hom`\|`prod`),
      `INTEGRATION_TESTS_BASE_URL` e
      `INTEGRATION_TESTS_PARAMETER_STORE_PATH` de variáveis de
      ambiente, e resolve `Cognito:UserPoolId` + nome da tabela
      DynamoDB (via `GetParametersByPath` em hom/prod; valores fixos
      locais de `appsettings.Development.json`/`docker-compose.yml`
      em `local`)
- [x] 3. Criar `Support/IApiTransport.cs` (interface + `TransportResponse`)
      e `Support/DirectHttpTransport.cs` (implementação `hom`/`prod`,
      `HttpClient` puro contra a `BaseUrl`)
- [x] 4. Criar `Support/LambdaRieTransport.cs` (implementação `local`) —
      monta `APIGatewayHttpApiV2ProxyRequest`, invoca
      `POST http://localhost:9000/2015-03-31/functions/function/invocations`,
      desserializa `APIGatewayHttpApiV2ProxyResponse` (incl.
      `isBase64Encoded`) de volta pra `TransportResponse`
- [x] 5. Criar `Support/ApiTransportFactory.cs` — escolhe a
      implementação de `IApiTransport` conforme
      `IntegrationTestEnvironment.Mode`
- [x] 6. Criar `Support/CpfGenerator.cs` — gera CPF sintético válido
      (dígito verificador correto) único por execução, evitando
      colisão com `CpfPointer` de execuções anteriores
- [x] 7. Criar `Support/TestAccountFixture.cs` — setup
      (`POST /auth/register` via `IApiTransport` com e-mail único +
      CPF do passo 6 → `AdminConfirmSignUpAsync` via
      `IAmazonCognitoIdentityProvider` apontando pro `ServiceURL`
      resolvido → `POST /auth/login` via `IApiTransport` pra obter o
      `accessToken`) e cleanup em `DisposeAsync` (roda sempre, mesmo
      com teste falhando): `Query PK=USER#<userId>` →
      `AccountPointer`/`UserProfile`; `Query PK=ACCOUNT#<accountId>` →
      todos os itens da conta (`Account`, `Membership`, 13 categorias
      padrão, itens criados pelo teste); `BatchWriteItem` apagando
      tudo; `DeleteItem` do `CpfPointer`; `AdminDeleteUserAsync` no
      Cognito
- [x] 8. Marcar `TestAccountFixture` e os testes que a usam com
      `[Trait("Category", "Integration")]`, pra permitir excluí-los do
      `dotnet test GastosApp.sln` genérico (ver Fase 4)

## Fase 2 — Testes do módulo Auth (primeiro módulo coberto)

- [x] 9. Implementar `Auth/AuthFlowTests.cs` — fluxo de sucesso:
      registro + confirmação + login retornando `accessToken` válido,
      usando `TestAccountFixture`
- [x] 10. Adicionar ao mesmo arquivo um teste de fluxo de erro mapeado
       em `backend/specs/FEAT-01-auth/spec.md` (ex.: login com senha
       errada → 401) contra a API real

## Fase 3 — Execução local (Docker + Native AOT + Runtime Interface Emulator)

- [x] 11. Criar `backend/infra/lambda/Dockerfile.local-run`
       (reaproveita o estágio `build` de `Dockerfile.build`; estágio
       final `FROM public.ecr.aws/lambda/provided:al2023`, copia o
       `bootstrap` e `appsettings.json` publicados)
- [x] 12. Declarar rede Docker nomeada (`gastosapp-local`) em
       `backend/infra/docker-compose.yml`, usada tanto por
       `localstack`/`cognito-local` quanto pelo container da FEAT-29,
       pra resolução de nome entre eles
- [x] 13. Criar `backend/infra/lambda/run-local.sh` — builda a imagem
       `local-run`; garante `docker compose up -d` se
       LocalStack/cognito-local não estiverem no ar; baixa/cacheia o
       binário `aws-lambda-rie` (versão pinada) em
       `backend/infra/lambda/.rie/` se ainda não existir; sobe o
       container (`docker run` com o RIE montado como entrypoint,
       porta `9000`, env vars apontando pra LocalStack/cognito-local);
       aguarda health-check (primeiro invoke de warm-up); roda
       `dotnet test tests/GastosApp.IntegrationTests -c Release --filter Category=Integration`
       com `INTEGRATION_TESTS_MODE=local`; desliga o container ao
       final, sempre (sucesso ou falha)
- [x] 14. Validar manualmente: rodar `run-local.sh` do zero (sem
       nenhum container no ar) e confirmar que `AuthFlowTests` passam
       contra o binário Native AOT publicado — validado, com um achado
       real corrigido no processo: `cognito-local` fixa `issuer`/
       `jwks_uri` em `localhost:9229`, exigindo
       `--network container:gastosapp-cognito-local` (namespace de
       rede compartilhado) em vez de só anexar à rede nomeada — ver
       plan.md, "Container local"

## Fase 4 — Isolar testes integrados do `dotnet test` padrão

- [x] 15. Adicionar `--filter "Category!=Integration"` aos comandos
       `dotnet test GastosApp.sln` já existentes em
       `backend-feature-pr.yml`, `backend-deploy-hom.yml`
       (job `quality`) e `backend-deploy-prod.yml` (job `quality`);
       confirmar que `dotnet test GastosApp.sln` continua rodando
       localmente sem Docker/rede, sem os testes da FEAT-29

## Fase 5 — Infraestrutura AWS (IAM) — aprovação já concedida, revisão de diff antes de aplicar

- [x] 16. Atualizar `backend/infra/terraform/cicd/iam-policy.tf` com
       as novas permissões na role `gastosapp-backend-cicd`:
       `cognito-idp:AdminConfirmSignUp`/`AdminDeleteUser` (escopadas
       aos ARNs dos User Pools hom/prod) e
       `dynamodb:Query`/`DeleteItem`/`BatchWriteItem` (escopadas aos
       ARNs das tabelas `GastosApp-Hom`/`GastosApp`); confirmar se
       `ssm:GetParametersByPath` já cobre os caminhos usados por este
       plano ou precisa de ajuste
- [ ] 17. Rodar `terraform plan` em `backend/infra/terraform/cicd/`,
       mostrar o diff ao usuário e só então `terraform apply`
       (`.tf` já escrito e validado — `fmt`/`validate` passam — mas
       `plan`/`apply` não rodaram: sem credenciais AWS válidas nesta
       sessão; SSO expirado, `aws sts get-caller-identity` falhou)

## Fase 6 — Workflows de CI/CD

- [x] 18. Adicionar o job `integration-tests` em
       `backend-deploy-hom.yml` (entre `deploy` e `draft-release`,
       `environment: backend-hom`, credenciais OIDC, roda
       `dotnet test tests/GastosApp.IntegrationTests -c Release` com
       `INTEGRATION_TESTS_MODE=hom`); atualizar `draft-release` para
       `needs: [deploy, integration-tests]`
- [x] 19. Adicionar o job `check-hom-integration-tests` em
       `backend-deploy-prod.yml` (entre `check-changes` e `quality`,
       usa `gh run list --workflow backend-deploy-hom.yml` pra
       confirmar execução `success` no commit da tag); atualizar
       `quality` para `needs: [check-changes, check-hom-integration-tests]`
- [x] 20. Criar `.github/workflows/backend-integration-tests-prod.yml`
       novo — só `workflow_dispatch`, sem gatilho automático, roda a
       suíte contra `https://api.jrnexpenses.com`

## Fase 7 — Validação ao vivo

Depende da Fase 5 (permissão IAM aplicada) e de PR/merge pra `develop`
(fora do escopo de uma sessão de implementação isolada — precisa do
fluxo normal de PR automático + merge manual do repositório).

- [ ] 21. Push em `develop` com uma mudança trivial no backend →
       confirmar que `backend-deploy-hom.yml` roda `integration-tests`
       com sucesso contra hom real (conta de teste criada e
       removida) e só então cria/atualiza o rascunho de release
- [ ] 22. Publicar a release de teste (`backend-v*`) → confirmar que
       `check-hom-integration-tests` encontra a execução bem-sucedida
       e libera `quality`/`deploy` em `backend-deploy-prod.yml`
- [ ] 23. Disparar manualmente `backend-integration-tests-prod.yml`
       pela aba Actions do GitHub → confirmar execução isolada contra
       produção, sem tocar build/deploy, com limpeza da conta de
       teste ao final

## Fase 8 — Documentação

- [x] 24. Atualizar `backend/docs/constitution.md` — nova regra:
       endpoint novo exige teste integrado (além do teste de
       componente já exigido desde a FEAT-03) como parte da
       definição de pronto
- [x] 25. Atualizar `backend/docs/backlog.md` — registrar débito
       técnico dos módulos ainda sem teste integrado (categorias,
       transações, membros, resumo, relatórios, export CSV, perfil),
       com contexto de que a infraestrutura (FEAT-29) já existe e só
       falta preencher caso a caso
- [x] 26. Atualizar `backend/CLAUDE.md` — estrutura de projetos
       (`GastosApp.IntegrationTests` deixa de ser "esqueleto, não
       usado hoje") e seção de convenções (teste integrado obrigatório
       pra endpoint novo)
- [x] 27. Atualizar `backend/infra/CLAUDE.md` — seção "CI/CD" com os
       novos jobs (`integration-tests`,
       `check-hom-integration-tests`) e o novo workflow
       (`backend-integration-tests-prod.yml`), e as novas permissões
       IAM da role `gastosapp-backend-cicd`

## Fase 9 — Fechamento

- [x] 28. Rodar `dotnet test GastosApp.sln -c Release --filter "Category!=Integration"`
       localmente e confirmar 100% dos testes (unitários + componente)
       passando
- [x] 29. Marcar em `backend/specs/FEAT-29-testes-integrados/spec.md`
       todos os critérios de aceite concluídos, refletindo o que foi
       de fato implementado e validado ao vivo
