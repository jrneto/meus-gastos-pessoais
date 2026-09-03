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

export class ForbiddenError extends Error {
  constructor() {
    super('Seu nível de acesso não permite esta ação.')
    this.name = 'ForbiddenError'
  }
}

export class UnknownTransactionError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownTransactionError'
  }
}

export class InvalidFilterError extends Error {
  constructor() {
    super('Um ou mais filtros são inválidos.')
    this.name = 'InvalidFilterError'
  }
}

export class UnknownTransactionQueryError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao buscar as transações. Tente novamente.')
    this.name = 'UnknownTransactionQueryError'
  }
}

export class NotFoundError extends Error {
  constructor() {
    super('Transação não encontrada.')
    this.name = 'NotFoundError'
  }
}

export class UpdateValidationError extends Error {
  constructor() {
    super('Não foi possível salvar as alterações. Verifique os dados informados.')
    this.name = 'UpdateValidationError'
  }
}
