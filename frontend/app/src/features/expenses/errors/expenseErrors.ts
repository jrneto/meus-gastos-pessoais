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

export class InvalidFilterError extends Error {
  constructor() {
    super('Um ou mais filtros são inválidos.')
    this.name = 'InvalidFilterError'
  }
}

export class UnknownExpenseQueryError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao buscar as despesas. Tente novamente.')
    this.name = 'UnknownExpenseQueryError'
  }
}

export class NotFoundError extends Error {
  constructor() {
    super('Despesa não encontrada.')
    this.name = 'NotFoundError'
  }
}

export class UpdateValidationError extends Error {
  constructor() {
    super('Não foi possível salvar as alterações. Verifique os dados informados.')
    this.name = 'UpdateValidationError'
  }
}
