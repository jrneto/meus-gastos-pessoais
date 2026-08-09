// Regex de tag semântica (ex.: v1.4.0) — usada tanto aqui quanto na
// trust policy de deploy (FEAT-09) quanto no cálculo de patch bump do
// job draft-release (FEAT-11). Sem mudança de comportamento; comentário
// atualizado (teste 2/2) só pra validar idempotência do draft-release.
const RELEASE_REGEX = /^v\d+\.\d+\.\d+$/
const REPO_URL = 'https://github.com/jrneto/meus-gastos-pessoais'

export interface AppVersionInfo {
  /** Tag semântica (ex.: "v1.4.0") em produção, "dev-<sha>" em homologação. */
  version: string
  commitSha: string
  /** true quando `version` é uma tag semântica publicada como GitHub Release. */
  isRelease: boolean
  /** true quando não há env vars de CI (ex.: `npm run dev` local) — sem commit real para linkar. */
  isLocal: boolean
  /** Link para a release (produção) ou para o commit (homologação). Vazio quando `isLocal`. */
  url: string
}

/**
 * Lê as variáveis de versão injetadas em build-time pelo CI
 * (`VITE_APP_VERSION`/`VITE_APP_COMMIT_SHA`, nunca versionadas — ver
 * frontend/specs/FEAT-09-cicd-github-actions/plan.md) e monta a
 * informação de rastreabilidade exibida no site.
 */
export function getAppVersion(): AppVersionInfo {
  const version = import.meta.env.VITE_APP_VERSION ?? 'dev-local'
  const commitSha = import.meta.env.VITE_APP_COMMIT_SHA ?? 'local'
  const isRelease = RELEASE_REGEX.test(version)
  const isLocal = !import.meta.env.VITE_APP_VERSION

  const url = isLocal
    ? ''
    : isRelease
      ? `${REPO_URL}/releases/tag/${version}`
      : `${REPO_URL}/commit/${commitSha}`

  return { version, commitSha, isRelease, isLocal, url }
}
