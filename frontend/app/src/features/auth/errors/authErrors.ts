export class InvalidCredentialsError extends Error {
  constructor() {
    super('Email ou senha inválidos.')
    this.name = 'InvalidCredentialsError'
  }
}

export class NetworkError extends Error {
  constructor() {
    super('Não foi possível conectar à API. Verifique sua conexão.')
    this.name = 'NetworkError'
  }
}

export class UnknownAuthError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownAuthError'
  }
}

export class RefreshFailedError extends Error {
  constructor() {
    super('Refresh token ausente, inválido ou expirado.')
    this.name = 'RefreshFailedError'
  }
}

export class EmailAlreadyExistsError extends Error {
  constructor() {
    super('Este email já está cadastrado.')
    this.name = 'EmailAlreadyExistsError'
  }
}

export class CpfAlreadyExistsError extends Error {
  constructor() {
    super('Este CPF já está cadastrado.')
    this.name = 'CpfAlreadyExistsError'
  }
}

export class RegisterValidationError extends Error {
  constructor() {
    super('Não foi possível concluir o cadastro. Verifique os dados informados.')
    this.name = 'RegisterValidationError'
  }
}

export class AccountPendingApprovalError extends Error {
  constructor() {
    super('Sua conta ainda não foi aprovada. Aguarde a confirmação do administrador e tente novamente.')
    this.name = 'AccountPendingApprovalError'
  }
}
