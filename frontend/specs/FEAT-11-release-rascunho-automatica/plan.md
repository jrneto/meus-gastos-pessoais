# Plano técnico — FEAT-11: Release de homologação automática (rascunho)

Consulte `spec.md` desta feature para requisitos e critérios de
aceite. Sem mudança em `frontend-deploy-prod.yml` (confirmado
explicitamente fora do escopo) — este plano só toca
`frontend-deploy-hom.yml`.

## Camadas afetadas

Feature 100% de CI/CD, sem código de app.

| Arquivo | Mudança |
|---|---|
| `.github/workflows/frontend-deploy-hom.yml` | Novo job `draft-release` (`needs: deploy`) |
| `frontend/app/src/**` | Sem mudança |

## Contratos técnicos

### Job `draft-release` (`frontend-deploy-hom.yml`)

```yaml
draft-release:
  needs: deploy
  runs-on: ubuntu-latest
  permissions:
    contents: write # necessário pra criar/deletar release (diferente de pull-requests: write da FEAT-10)
  steps:
    - uses: actions/checkout@v4

    - name: Criar/atualizar rascunho de release
      env:
        GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        set -e

        # 1) Versão sugerida = patch bump da última release PUBLICADA
        #    (exclui rascunhos — um rascunho pendente não conta como
        #    referência de versão)
        last_tag=$(gh release list --exclude-drafts --limit 1 \
          --json tagName --jq '.[0].tagName // ""')

        if [ -z "$last_tag" ]; then
          new_tag="v0.0.1"
        else
          version="${last_tag#v}"
          IFS='.' read -r major minor patch <<< "$version"
          new_tag="v${major}.${minor}.$((patch + 1))"
        fi

        # 2) Se já existe um rascunho pendente, remove antes de criar o
        #    novo — garante que nunca haja mais de um rascunho ao mesmo
        #    tempo (US4 da spec)
        existing_draft=$(gh release list --json tagName,isDraft \
          --jq '[.[] | select(.isDraft)] | .[0].tagName // ""')

        if [ -n "$existing_draft" ]; then
          echo "Removendo rascunho pendente: $existing_draft"
          gh release delete "$existing_draft" --yes
        fi

        # 3) Cria o novo rascunho, target = develop, notas geradas
        #    automaticamente a partir dos PRs mergeados desde a última
        #    release publicada (recurso nativo do GitHub)
        gh release create "$new_tag" \
          --draft \
          --target develop \
          --title "$new_tag" \
          --generate-notes
```

- `gh release list --exclude-drafts` — só releases publicadas contam
  como referência pra calcular o próximo patch.
- `gh release create --generate-notes` sem `--notes-start-tag`
  explícito: o GitHub já detecta sozinho a última release publicada
  como ponto de partida do changelog (mesmo mecanismo usado por
  `last_tag` acima, mas nativo — não precisamos repassar).
- Delete-then-create (em vez de `gh release edit`) escolhido por
  simplicidade e previsibilidade — ver "Decisões técnicas".

## Decisões técnicas e trade-offs

- **Delete + recreate em vez de `gh release edit`**: `gh release edit`
  tem suporte inconsistente a `--generate-notes` entre versões da CLI
  (varia por versão do `gh` pré-instalado no runner `ubuntu-latest`,
  que muda ao longo do tempo sem controle do projeto). Apagar o
  rascunho antigo e criar um novo do zero é mais previsível: sempre
  usa o caminho `gh release create`, testado e documentado.
- **Sem `--notes-start-tag` manual**: o GitHub já calcula sozinho a
  release publicada anterior como base do changelog automático —
  replicar essa lógica manualmente seria redundante e um ponto a mais
  de manutenção.
- **`contents: write` escopado só neste job**: os outros jobs
  (`quality`, `deploy`) continuam com o mínimo necessário — mesmo
  princípio de menor privilégio já usado na FEAT-10.
- **Fallback `v0.0.1` quando não há release publicada**: cenário hoje
  não deve ocorrer (já existe `v0.1.0` publicada desde a FEAT-09), mas
  o job não pode quebrar se rodar antes de qualquer release existir
  (ex.: um fork ou reset do projeto).
- **Sem retry/tratamento especial de erro no `gh release delete`**:
  se falhar (ex.: rascunho já foi deletado manualmente entre a
  checagem e a exclusão — corrida improvável dado que só este job
  mexe em rascunhos), o job inteiro falha e loga o erro — aceitável
  pro volume de uso deste projeto, sem necessidade de lógica de retry.

## Recursos AWS

**Nenhum.** Só GitHub Actions + API nativa de Releases do GitHub via
`gh` CLI.

## Tratamento de falhas

| Etapa que falha | Efeito |
|---|---|
| `deploy` (hom) falha | Job `draft-release` nunca roda (`needs: deploy`) — nenhum rascunho é criado/atualizado para um deploy que não foi ao ar |
| `gh release list`/`gh release create`/`gh release delete` falha (ex.: permissão, rate limit) | Job falha, sem afetar hom (já publicado) nem qualquer release/rascunho existente além do que o comando específico tentou tocar |

Sem rollback ou notificação adicional — mesmo padrão já estabelecido
nas features anteriores de CI/CD.

## Pontos que precisam de confirmação do usuário antes do `/tasks`

1. **Validação real depende de um push em `develop`** (não é
   simulável localmente — não há `gh` CLI disponível no ambiente de
   execução deste agente). A implementação vai seguir o mesmo padrão
   de bootstrap da FEAT-10: um push trivial em `frontend/app/**` nesta
   própria branch dispara `frontend-deploy-hom.yml` (que já existe) e
   valida o novo job.
2. **Título do rascunho** = a própria tag sugerida (ex.: `v0.1.1`),
   sem texto adicional tipo "(rascunho automático)" — confirmar se
   serve assim ou se prefere algo mais descritivo (o rascunho já é
   visualmente marcado como "Draft" pelo GitHub, então achei redundante
   repetir isso no título).
