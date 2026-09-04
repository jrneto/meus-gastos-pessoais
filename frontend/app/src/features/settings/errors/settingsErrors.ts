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

export class UnknownExportError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao exportar. Tente novamente.')
    this.name = 'UnknownExportError'
  }
}
