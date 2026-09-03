import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownExportError } from '../errors/settingsErrors'
import { settingsApi } from './settingsApi'

const EXPORT_URL = 'http://localhost:5049/transactions/export'

const CSV_BODY = 'data;descricao;categoria;tipo;valor;lancadoPor\r\n2026-08-15;Almoço;Alimentacao;despesa;45,90;Você\r\n'

describe('settingsApi.exportTransactionsCsv', () => {
  it('envia o Authorization correto e devolve o Blob do corpo em caso de sucesso', async () => {
    let receivedAuth: string | null = null
    server.use(
      http.get(EXPORT_URL, ({ request }) => {
        receivedAuth = request.headers.get('Authorization')
        return HttpResponse.text(CSV_BODY, { headers: { 'Content-Type': 'text/csv; charset=utf-8' } })
      }),
    )

    const result = await settingsApi.exportTransactionsCsv('tok-123')

    expect(receivedAuth).toBe('Bearer tok-123')
    // Não usa `toBeInstanceOf(Blob)` — o `Blob` global do processo de
    // teste pode ser uma classe diferente do `Blob` retornado por
    // `Response.blob()` dependendo da versão do Node/runtime (achado
    // em CI, Node 22, ausente localmente em Node 24), então checa o
    // formato (duck typing) + o conteúdo, que é o que importa de
    // verdade pro `downloadBlob` funcionar.
    expect(typeof result.text).toBe('function')
    expect(await result.text()).toBe(CSV_BODY)
  })

  it('em caso de 401, lança SessionExpiredError', async () => {
    server.use(http.get(EXPORT_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(settingsApi.exportTransactionsCsv('tok-123')).rejects.toBeInstanceOf(SessionExpiredError)
  })

  it('em caso de falha de rede, lança NetworkError', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.error()))

    await expect(settingsApi.exportTransactionsCsv('tok-123')).rejects.toBeInstanceOf(NetworkError)
  })

  it('em caso de outro status de erro, lança UnknownExportError', async () => {
    server.use(http.get(EXPORT_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(settingsApi.exportTransactionsCsv('tok-123')).rejects.toBeInstanceOf(UnknownExportError)
  })
})
