const API_BASE_URL = import.meta.env.VITE_API_BASE_URL

async function request(path: string, init?: RequestInit): Promise<Response> {
  return fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })
}

export const httpClient = {
  get: (path: string, init?: RequestInit) => request(path, { ...init, method: 'GET' }),
  post: (path: string, body?: unknown, init?: RequestInit) =>
    request(path, {
      ...init,
      method: 'POST',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  put: (path: string, body?: unknown, init?: RequestInit) =>
    request(path, {
      ...init,
      method: 'PUT',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
}
