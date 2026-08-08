/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  // Injetadas só em CI (workflows de deploy), nunca versionadas em
  // .env* — ver frontend/specs/FEAT-09-cicd-github-actions/plan.md
  readonly VITE_APP_VERSION?: string
  readonly VITE_APP_COMMIT_SHA?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
