import { getAppVersion } from '@/lib/appVersion'

/**
 * Exibe a versão do build publicado, como texto simples (sem link) —
 * ver frontend/specs/FEAT-09-cicd-github-actions/spec.md.
 */
export function AppVersion() {
  return <p className="text-muted-foreground text-sm">Versão: {getAppVersion()}</p>
}
