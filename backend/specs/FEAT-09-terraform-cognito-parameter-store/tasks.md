# FEAT-09: Tasks — Cognito e Parameter Store sob Terraform

Nenhuma task de `apply`/`import`/exclusão roda sem aprovação explícita
do usuário no momento da execução (ver `plan.md`). Tasks 8–9 (exclusão
do recurso antigo) **não são executadas por mim em nenhuma hipótese** —
ficam de fora deste checklist de implementação e são de responsabilidade
exclusiva do usuário, no momento em que ele decidir.

- [x] 1. Criar `backend/infra/terraform/cognito.tf` com o recurso
      `aws_cognito_user_pool "main"` (`name = "user-pool-gastos-app"`,
      password policy, `mfa_configuration = "OFF"`,
      `deletion_protection = "ACTIVE"`, `account_recovery_setting`,
      `username_attributes`/`auto_verified_attributes = ["email"]`,
      `schema` do atributo `email`), conforme `plan.md`
- [x] 2. Adicionar em `cognito.tf` o recurso
      `aws_cognito_user_pool_client "spa"` (`name =
      "controle-gastos-spa"`, sem secret, fluxos de auth, `callback_urls`
      com o placeholder atual, validade de tokens), conforme `plan.md`
- [x] 3. Criar `backend/infra/terraform/parameter-store.tf` com os 3
      recursos `aws_ssm_parameter` (`UserPoolId`, `ClientId`, `Region`
      sob `/GastosApp/Cognito/`), com `value` referenciando os outputs
      do novo User Pool/App Client (`aws_cognito_user_pool.main.id`,
      `aws_cognito_user_pool_client.spa.id`, `var.aws_region`)
- [x] 4. Rodar `terraform plan` e apresentar o resultado ao usuário —
      deve mostrar apenas a **criação** do novo User Pool e App Client
      (nenhum recurso existente tocado) — aguardar aprovação explícita
- [x] 5. Após aprovação, rodar `terraform apply` para criar o novo User
      Pool e App Client
- [x] 6. Rodar `terraform import` dos 3 parâmetros existentes
      (`/GastosApp/Cognito/UserPoolId`, `.../ClientId`, `.../Region`)
      para os recursos criados na task 3
- [x] 7. Rodar `terraform plan` e apresentar o resultado ao usuário —
      deve mostrar a **atualização in-place** dos 3 parâmetros para os
      novos `UserPoolId`/`ClientId` (sem recriação, `name`/`type`
      inalterados) — aguardar aprovação explícita
- [x] 8. Após aprovação, rodar `terraform apply` para atualizar os 3
      parâmetros
- [x] 9. Reiniciar a API local (`dotnet run --project src/GastosApp.Api`)
      para recarregar `CognitoOptions` com os novos valores
- [x] 10. Validar manualmente: `POST /auth/register` com um usuário de
       teste contra o novo User Pool, seguido de `POST /auth/login`,
       confirmando token válido e `GET /auth/me` funcionando
- [x] 11. Atualizar `backend/infra/terraform/README.md` descrevendo os
       novos arquivos `cognito.tf`/`parameter-store.tf` e que Cognito e
       Parameter Store agora são geridos por Terraform
- [x] 12. Atualizar `backend/infra/CLAUDE.md`, removendo a menção de que
       Cognito e Parameter Store ficam fora do Terraform "até serem
       migrados explicitamente"
- [x] 13. Atualizar `backend/docs/constitution.md`, removendo/ajustando
       a regra imutável equivalente sobre Cognito/Parameter Store fora
       do Terraform
- [x] 14. Atualizar `backend/specs/FEAT-09-terraform-cognito-parameter-store/spec.md`,
       marcando como concluídos os critérios de aceite já validados
       (state list inclui os recursos, `terraform plan` sem diferenças
       para os recursos criados/importados, `ClientId`/`UserPoolId`
       atualizados na configuração do backend, documentação atualizada)
       — os critérios que dependem da exclusão manual do pool antigo
       (task 8 do `plan.md`, fora deste checklist) ficam marcados como
       pendentes até o usuário confirmar que a exclusão foi feita

## Fora deste checklist (responsabilidade exclusiva do usuário)

- Desativar `DeletionProtection` e excluir manualmente o User Pool
  antigo (`us-east-1_cvKHaKo0g`)
- Rodar o `terraform plan` final de confirmação ("No changes") após essa
  exclusão, quando o usuário decidir — pode me pedir para rodar esse
  comando específico nesse momento, mas não antes
