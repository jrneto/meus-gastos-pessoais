export class SessionExpiredError extends Error {
  constructor() {
    super('Sua sessão expirou. Faça login novamente.')
    this.name = 'SessionExpiredError'
  }
}

export class NetworkError extends Error {
  constructor() {
    super('Não foi possível conectar à API. Verifique sua conexão.')
    this.name = 'NetworkError'
  }
}

export class UnknownReportsError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao carregar o relatório. Tente novamente.')
    this.name = 'UnknownReportsError'
  }
}
