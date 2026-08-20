# Emula a API do Amazon Cognito para desenvolvimento local (FEAT-18).
# LocalStack Community não suporta Cognito (recurso exclusivo da edição
# paga) — cognito-local (https://github.com/jagregory/cognito-local) é
# um projeto open-source que expõe a mesma API HTTP do Cognito real
# (sign-up, USER_PASSWORD_AUTH, JWKS), permitindo usar o AWS SDK já
# existente em GastosApp.Infrastructure sem nenhuma reescrita — só
# muda o endpoint configurado (Cognito:ServiceURL).
#
# Build próprio a partir do pacote npm em vez de uma imagem pronta de
# terceiros no Docker Hub: reduz o risco de depender de uma imagem
# desatualizada/não mantida.
FROM node:20-alpine

# Versão fixa — atualizar deliberadamente quando necessário.
RUN npm install -g cognito-local@5.3.0

WORKDIR /app
COPY config.json /app/config.default.json
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Diretório de estado do cognito-local (user pools, usuários) —
# montado como volume no docker-compose.yml para persistir entre
# reinícios do container.
VOLUME ["/app/.cognito"]

EXPOSE 9229

ENTRYPOINT ["/app/entrypoint.sh"]
