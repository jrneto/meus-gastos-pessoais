# FEAT-34 — Tasks

Checklist derivado de [`plan.md`](plan.md) (mecanismo em §3, etapas em
§4). Legenda de repositório: **[infra]** = `D:\git_jrneto\infra-jrnexpenses`
(commits direto em `develop`); **[mono]** = este monorepo, branch
`FEAT-34-extracao-infra-repo-apartado`. **[APROVAÇÃO]** = comando que
altera state remoto ou recurso real — só roda depois do usuário aprovar
explicitamente aquele comando, naquele momento. `plan`, `state pull`,
`state list` e `state show` são leitura e não precisam.

Invariante de toda etapa: antes de começar, `terraform plan` da config
de origem no monorepo tem que dar "No changes" (baseline); ao terminar,
"No changes" nos dois lados. Qualquer `create`/`destroy`/`replace`/
`update` de recurso em qualquer `plan` é bloqueio — parar e investigar.

## Etapa 0 — esqueleto do `infra-jrnexpenses` (parte manual já feita)

- [ ] 1. [infra] Acrescentar `.terraform.lock.hcl` ao `.gitignore`
      (template do GitHub não cobre; prática do monorepo mantida)
- [ ] 2. [infra] Criar `CLAUDE.md`: governança (apply manual/local,
      aprovação por execução, nada de Terraform em CI), bucket de state
      e mapa de keys (`infra-jrnexpenses/{dns,hom,prod}/terraform.tfstate`),
      ordem de dependência (`environments/{hom,prod}` → `dns/`;
      monorepo → `environments/{hom,prod}`), princípio "infra nunca lê
      state do monorepo", guardrail IAM do perfil `agent-toolkit`,
      assinatura manual ao plano Free do CloudFront, fluxo de branches
      (`develop` → PR manual → `main`)
- [ ] 3. [infra] Criar `README.md`: propósito do repo, layout
      `terraform/{cicd/{frontend,backend},dns,environments/{hom,prod}}`,
      comandos de `init` (parcial, `-backend-config`) / `plan` / `apply`
      por config, seção "Migração a partir do monorepo" (o que veio de
      onde, link para as specs FEAT-34/FEAT-40), placeholder para a
      seção `cicd/`
- [ ] 4. [infra] Criar `scripts/migration/.gitkeep` e
      `terraform/.gitkeep`; commit em `develop`
      ("chore: esqueleto do repositório") e push

## Etapa 1 — `cicd/` do frontend (só arquivos, nenhum comando Terraform)

- [ ] 5. [infra] Copiar `frontend/infra/terraform/cicd/*.tf` (6 arquivos)
      para `terraform/cicd/frontend/` sem alterar conteúdo (key
      `gastosapp-frontend/cicd/terraform.tfstate` mantida — state vazio)
- [ ] 6. [infra] Mover a seção "cicd/ — OIDC Provider + IAM Role" de
      `frontend/infra/terraform/README.md` para o `README.md` da infra
      (ARNs, motivo do guardrail, comandos de `import` futuros); commit
      + push
- [ ] 7. [mono] Remover `frontend/infra/terraform/cicd/`; no
      `frontend/infra/terraform/README.md` trocar a seção por um
      ponteiro para o repo novo; em `frontend/infra/CLAUDE.md` ajustar o
      gotcha "OIDC Provider + Role fora do Terraform" para apontar o
      caminho novo; commit

## Etapa 2 — `dns/` (state `gastosapp-frontend/dns/` → `infra-jrnexpenses/dns/`)

- [ ] 8. [mono] Baseline: `terraform init` + `terraform plan` em
      `frontend/infra/terraform/dns/` = "No changes"; `terraform state
      list` salvo como referência (esperado: `aws_route53_zone.main` +
      9 instâncias de `aws_route53_record`)
- [ ] 9. [infra] Criar `terraform/dns/`: `route53.tf` e
      `remote_state.tf` copiados sem alteração; `versions.tf` com key
      `infra-jrnexpenses/dns/terraform.tfstate`; `variables.tf` copiado
      (keys **ainda** apontando para `gastosapp-frontend/{hom,prod}/…`)
- [ ] 10. [infra] `terraform init` em `terraform/dns/` (state remoto
      vazio) e `terraform validate`
- [ ] 11. [infra] Criar `scripts/migration/wave-2-dns.sh` com o loop de
      `terraform state mv -state=mono.tfstate -state-out=infra.tfstate
      "$addr" "$addr"` para: `aws_route53_zone.main`,
      `aws_route53_record.apex_a`, `.apex_aaaa`, `.www_a`, `.www_aaaa`,
      `.acm_validation`, `.hom_a`, `.hom_aaaa`, `.acm_validation_hom`
      (endereço do recurso, sem índice — move todas as instâncias);
      usuário revisa a lista contra o `state list` da task 8
- [ ] 12. Preparar pasta scratch (`/tmp/tf-mv`, `versions.tf` só com
      provider, `terraform init`); `state pull` do monorepo `dns/` →
      `mono.tfstate` + cópia de backup datada; `state pull` da infra
      `dns/` → `infra.tfstate`
- [ ] 13. Rodar `wave-2-dns.sh` no scratch; conferir `terraform state
      list -state=infra.tfstate` (10 entradas) e `-state=mono.tfstate`
      (só `data.*`)
- [ ] 14. [infra] **[APROVAÇÃO]** `terraform state push infra.tfstate`
      em `terraform/dns/` (`-force` só se reclamar de lineage);
      `terraform plan` = "No changes" (dns não tem outputs → sem apply)
- [ ] 15. [mono] **[APROVAÇÃO]** `terraform state push mono.tfstate` em
      `frontend/infra/terraform/dns/` (state fica só com `data.*`);
      confirmar com `terraform state list`
- [ ] 16. [mono] Remover `frontend/infra/terraform/dns/` inteira;
      atualizar `frontend/infra/terraform/README.md` (seção `dns/` vira
      ponteiro; ordem de execução sem o passo 2) e `frontend/infra/
      CLAUDE.md` ("Estado atual": `dns/` vive no repo novo); commit
- [ ] 17. [infra] Commit "feat(dns): migra hosted zone e records do
      monorepo (FEAT-34 etapa 2)" com `terraform/dns/` +
      `scripts/migration/wave-2-dns.sh` + README (registro do que foi
      movido); push
- [ ] 18. Registrar no `README.md` da infra que
      `gastosapp-frontend/dns/terraform.tfstate` ficou órfão (vazio) —
      limpeza ao final da FEAT-40 (decisão §7.3)

## Etapa 3 — frontend hom (`gastosapp-frontend/hom/` → `infra-jrnexpenses/hom/`)

- [ ] 19. [mono] Baseline: `terraform plan` em
      `frontend/infra/terraform/environments/hom/` = "No changes";
      `terraform state list` salvo (esperado: 4 S3 + OAC + distribuição
      + ACM + WAF); `terraform state show aws_cloudfront_distribution.main`
      salvo para conferir `origin.domain_name` depois
- [ ] 20. [infra] Criar `terraform/environments/hom/`: `versions.tf`
      (key `infra-jrnexpenses/hom/terraform.tfstate`), `variables.tf`
      (`aws_region`, `hom_domain_name`, `frontend_bucket_name`),
      `frontend-acm.tf` e `frontend-waf.tf` copiados sem alteração de
      conteúdo, `frontend-data.tf` (`data "aws_s3_bucket" "frontend"`),
      `frontend-cloudfront.tf` com a única mudança
      `origin.domain_name = data.aws_s3_bucket.frontend.bucket_regional_domain_name`,
      `frontend-outputs.tf` (os 3 outputs atuais +
      `cloudfront_distribution_arn` + `cloudfront_distribution_id`)
- [ ] 21. [infra] `terraform init` + `terraform validate` em
      `terraform/environments/hom/`
- [ ] 22. [infra] Criar `scripts/migration/wave-3-frontend-hom.sh`
      (4 endereços: `aws_cloudfront_origin_access_control.frontend`,
      `aws_cloudfront_distribution.main`, `aws_acm_certificate.hom`,
      `aws_wafv2_web_acl.hom`); usuário revisa
- [ ] 23. Scratch: `state pull` do monorepo hom → `mono.tfstate` +
      backup; `state pull` da infra hom → `infra.tfstate`; rodar o
      script; conferir `state list` dos dois arquivos
- [ ] 24. [infra] **[APROVAÇÃO]** `state push infra.tfstate` em
      `environments/hom/`; `terraform plan` → esperado **nenhum recurso**
      e só "Changes to Outputs" (5 outputs); conferir em especial que
      `aws_cloudfront_distribution.main` não mostra diff em
      `origin.domain_name`
- [ ] 25. [infra] **[APROVAÇÃO]** `terraform apply` só de outputs;
      `terraform plan` = "No changes"; `terraform output
      cloudfront_distribution_arn` bate com o ARN de `ELE195A1APCLB`
- [ ] 26. [mono] Editar `frontend/infra/terraform/environments/hom/`:
      apagar `acm.tf`, `cloudfront.tf`, `waf.tf`; `variables.tf` remove
      `hom_domain_name`, adiciona `state_bucket` e `infra_state_key`
      (default `infra-jrnexpenses/hom/terraform.tfstate`); novo
      `remote_state.tf` (`data "terraform_remote_state" "infra"`);
      `s3.tf` troca `aws_cloudfront_distribution.main.arn` por
      `data.terraform_remote_state.infra.outputs.cloudfront_distribution_arn`;
      `outputs.tf` passa a expor só `bucket_name` e `bucket_arn`;
      `terraform validate`
- [ ] 27. [mono] **[APROVAÇÃO]** `state push mono.tfstate` em
      `environments/hom/`; `terraform plan` → esperado nenhum recurso
      (bucket policy sem diff) e só outputs removidos/adicionados
- [ ] 28. [mono] **[APROVAÇÃO]** `terraform apply` só de outputs;
      `terraform plan` = "No changes"
- [ ] 29. [infra] `terraform/dns/variables.tf`: `hom_state_key` →
      `infra-jrnexpenses/hom/terraform.tfstate`; `terraform plan` em
      `dns/` = "No changes"
- [ ] 30. Smoke manual: `https://hom.jrnexpenses.com` carrega, F5 em
      rota interna (ex.: `/transactions`) devolve o app (fallback SPA),
      certificado válido; `aws cloudfront get-distribution --id
      ELE195A1APCLB` com `Status: Deployed`
- [ ] 31. [mono] Atualizar `frontend/infra/terraform/README.md` e
      `frontend/infra/CLAUDE.md` para hom (monorepo = só bucket +
      policy; CloudFront/ACM/WAF no repo novo; `remote_state` lido);
      commit
- [ ] 32. [infra] Commit "feat(hom): migra CloudFront/OAC/ACM/WAF do
      frontend (FEAT-34 etapa 3)" com `environments/hom/`, `dns/`
      repontada, script da etapa e README; push
- [ ] 33. Validação de pipeline: no próximo push em `develop` tocando
      `frontend/app/**` (não há `workflow_dispatch`), confirmar
      `frontend-deploy-hom.yml` verde (sync + invalidation) — pode
      acontecer depois desta etapa, mas **antes** de iniciar a etapa 4

## Etapa 4 — frontend prod (`gastosapp-frontend/prod/` → `infra-jrnexpenses/prod/`)

Só começa com a etapa 3 concluída, task 33 verde e **janela combinada
com o usuário**.

- [ ] 34. [mono] Baseline: `terraform plan` em `environments/prod/` =
      "No changes"; `state list` e `state show
      aws_cloudfront_distribution.main` salvos
- [ ] 35. [infra] Criar `terraform/environments/prod/`: `versions.tf`
      (key `infra-jrnexpenses/prod/terraform.tfstate`), `variables.tf`
      (`aws_region`, `domain_name`, `frontend_bucket_name`),
      `frontend-acm.tf`/`frontend-waf.tf` copiados sem alteração,
      `frontend-data.tf`, `frontend-cloudfront.tf` (única mudança em
      `origin.domain_name`; `origin_id` literal do console mantido),
      `frontend-outputs.tf` (5 outputs, nomes lógicos de prod:
      `aws_acm_certificate.frontend`)
- [ ] 36. [infra] `terraform init` + `terraform validate` em
      `environments/prod/`
- [ ] 37. [infra] Criar `scripts/migration/wave-4-frontend-prod.sh`
      (`aws_cloudfront_origin_access_control.frontend`,
      `aws_cloudfront_distribution.main`, `aws_acm_certificate.frontend`,
      `aws_wafv2_web_acl.frontend`); usuário revisa
- [ ] 38. Scratch: pulls + backup, rodar o script, conferir `state list`
- [ ] 39. [infra] **[APROVAÇÃO]** `state push` em `environments/prod/`;
      `terraform plan` revisado **linha a linha** — nenhum recurso,
      só outputs; distribuição `E2YCZNS0F94SCU` sem diff
- [ ] 40. [infra] **[APROVAÇÃO]** `apply` só de outputs; `plan` = "No
      changes"
- [ ] 41. [mono] Editar `environments/prod/` (mesmas mudanças da task
      26, `variables.tf` remove `domain_name`, `infra_state_key` default
      `infra-jrnexpenses/prod/terraform.tfstate`); `terraform validate`
- [ ] 42. [mono] **[APROVAÇÃO]** `state push` em `environments/prod/`;
      `plan` → nenhum recurso, só outputs
- [ ] 43. [mono] **[APROVAÇÃO]** `apply` só de outputs; `plan` = "No
      changes"
- [ ] 44. [infra] `dns/variables.tf`: `prod_state_key` →
      `infra-jrnexpenses/prod/terraform.tfstate`; `plan` em `dns/` =
      "No changes"
- [ ] 45. Smoke manual em `https://jrnexpenses.com` e
      `https://www.jrnexpenses.com` (carrega, F5 em rota interna,
      certificado válido)
- [ ] 46. [mono] `frontend/infra/terraform/README.md` e
      `frontend/infra/CLAUDE.md` atualizados para prod; commit
- [ ] 47. [infra] Commit "feat(prod): migra CloudFront/OAC/ACM/WAF do
      frontend (FEAT-34 etapa 4)"; push

## Etapa 7 (parte do frontend) — fechamento

- [ ] 48. [mono] Reescrita final de `frontend/infra/CLAUDE.md` e
      `frontend/infra/terraform/README.md` para o estado alvo (monorepo
      gerencia só bucket/PAB/SSE/policy por ambiente; tudo o mais
      aponta para `infra-jrnexpenses`; como rodar `init`/`plan` das duas
      configs restantes; nota de que `/CLAUDE.md` raiz e
      `/docs/architecture.md` são atualizados pela FEAT-40)
- [ ] 49. [mono] Conferir que nada em `.github/workflows/*` nem nos
      GitHub Environments `hom`/`prod` foi alterado (`git diff develop
      -- .github/` vazio)
- [ ] 50. [mono] Atualizar `spec.md` marcando os critérios de aceite
      concluídos (`- [x]`) e adicionar seção "Status" com data, o que
      foi movido, states órfãos pendentes de limpeza e achados
- [ ] 51. [mono] `frontend/docs/backlog.md`: marcar FEAT-34 como
      concluída; commit
- [ ] 52. [infra] Abrir PR `develop → main` no `infra-jrnexpenses`
      (manual) com resumo das 3 migrações de state; merge manual pelo
      usuário
- [ ] 53. [mono] Abrir PR `FEAT-34-extracao-infra-repo-apartado → develop`
      manualmente (o workflow `frontend-feature-pr.yml` não dispara para
      mudanças fora de `frontend/app/**`); merge manual

## Testes

Não há código de aplicação nesta feature — a suíte Vitest do
`frontend/app/` não é afetada e continua obrigatoriamente verde nos
PRs. O "teste" de cada etapa é o par `terraform plan` = "No changes"
(infra e monorepo) + smoke manual, já embutido nas tasks acima.
