# FEAT-10: Tasks — Deploy Lambda (Native AOT) + API Gateway

Nenhuma task de `terraform apply`/build de imagem/deploy roda sem
aprovação explícita do usuário no momento da execução (ver `plan.md`).

- [x] 1. Habilitar `<PublishAot>true</PublishAot>` e
      `<InvariantGlobalization>true</InvariantGlobalization>` em
      `GastosApp.Api.csproj`
- [x] 2. Rodar um build/publish Native AOT local via Docker (container
      efêmero, sem Terraform ainda) só para validar compatibilidade —
      resolver caso a caso qualquer erro de trimming encontrado
      (`System.Text.Json`, `Microsoft.AspNetCore.OpenApi`,
      `Scalar.AspNetCore`), conforme discutido no `plan.md`
- [x] 3. Corrigir `AwsParameterStoreExtensions.cs` para não forçar
      `Profile = "default"` quando rodando dentro de uma Lambda
      (detectar via `AWS_LAMBDA_FUNCTION_NAME`)
- [x] 4. Adicionar `builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi)`
      em `Program.cs`
- [x] 5. Rodar `dotnet build`/`dotnet test` (build normal, não-AOT) para
      confirmar que nada quebrou localmente com as mudanças acima
- [x] 6. Criar `backend/infra/lambda/Dockerfile.build` (multi-stage,
      `dotnet publish -r linux-x64 --self-contained -p:PublishAot=true`)
- [x] 7. Criar script de empacotamento (`backend/infra/lambda/build.sh`
      ou `.ps1`): builda a imagem, extrai o `bootstrap`, gera
      `function.zip`
- [x] 8. Rodar o script de empacotamento e confirmar que `function.zip`
      é gerado corretamente (só local, nenhum recurso AWS envolvido
      ainda)
- [x] 9. Criar `backend/infra/terraform/lambda.tf` (`aws_iam_role`,
      `aws_iam_role_policy` com as permissões scoped por ARN,
      `aws_cloudwatch_log_group` com `retention_in_days = 14`,
      `aws_lambda_function` apontando para `function.zip`, 256MB/10s/x86_64)
- [x] 10. Criar `backend/infra/terraform/api-gateway.tf`
       (`aws_apigatewayv2_api` com CORS, `aws_apigatewayv2_integration`,
       `aws_apigatewayv2_route "ANY /{proxy+}"`,
       `aws_apigatewayv2_stage` com throttling 5/10,
       `aws_lambda_permission`)
- [x] 11. Adicionar variável `frontend_origin` em `variables.tf` e
       output `api_gateway_url` em `outputs.tf`
- [x] 12. Rodar `terraform plan` e apresentar o resultado ao usuário —
       deve mostrar apenas criação de recursos novos (Lambda, IAM,
       log group, API Gateway), nada existente alterado/destruído —
       aguardar aprovação explícita
- [x] 13. Após aprovação, rodar `terraform apply`
- [x] 14. Validar manualmente contra a URL pública do API Gateway
       (`api_gateway_url`): `POST /auth/register`, `POST /auth/login`,
       `GET /auth/me`, e um fluxo de `/expenses/*` autenticado
- [x] 15. Validar manualmente: requisição a `/expenses/*` sem JWT
       retorna 401; uma rajada de requisições acima do throttling
       configurado retorna 429
- [x] 16. Conferir no CloudWatch Logs que a Lambda está logando
       corretamente (Serilog console → CloudWatch) e que o log group
       tem a retenção de 14 dias aplicada
- [x] 17. Rodar a suíte completa de testes (`dotnet test`) uma última
       vez para confirmar ausência de regressão após todas as mudanças
- [x] 18. Atualizar `backend/infra/terraform/README.md` descrevendo
       `lambda.tf`/`api-gateway.tf` e o processo de build/deploy
       (Docker + script de empacotamento)
- [x] 19. Atualizar `backend/docs/architecture.md`, marcando a seção
       "Infraestrutura de produção (fase futura)" como implementada
- [x] 20. Atualizar `backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/spec.md`,
       marcando os critérios de aceite concluídos com base na validação
       manual e nos resultados do `terraform plan`/`apply`