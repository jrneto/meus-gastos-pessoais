export class ValidationError extends Error {
  constructor() {
    super('Não foi possível registrar a despesa. Verifique os dados informados.')
    this.name = 'ValidationError'
  }
}

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

export class UnknownExpenseError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownExpenseError'
  }
}
