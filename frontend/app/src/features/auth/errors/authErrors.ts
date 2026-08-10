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
