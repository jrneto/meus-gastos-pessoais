import { getAppVersion } from '@/lib/appVersion'

/**
 * Exibe a versão publicada (release em produção, commit em
 * homologação) com link para o GitHub — ver
 * frontend/specs/FEAT-09-cicd-github-actions/spec.md.
 */
export function AppVersion() {
  const { version, isLocal, url } = getAppVersion()

  return (
    <p className="text-muted-foreground text-sm">
      Versão:{' '}
      {isLocal ? (
        version
      ) : (
        <a href={url} target="_blank" rel="noreferrer" className="underline hover:no-underline">
          {version}
        </a>
      )}
    </p>
  )
}
