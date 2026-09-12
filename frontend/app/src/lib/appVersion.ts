/**
 * Lê a versão injetada em build-time pelo CI (`VITE_APP_VERSION`, nunca
 * versionada — ver frontend/specs/FEAT-09-cicd-github-actions/plan.md).
 * Fallback `dev-local` em execução local (ex.: `npm run dev`), onde
 * essa env var nunca é setada.
 */
export function getAppVersion(): string {
  return import.meta.env.VITE_APP_VERSION ?? 'dev-local'
}

// Touch trivial para validar frontend-deploy-hom.yml após a migração de
// infra da FEAT-34 etapa 3 (CloudFront/OAC/ACM/WAF movidos para
// infra-jrnexpenses) — sem mudança de comportamento.
