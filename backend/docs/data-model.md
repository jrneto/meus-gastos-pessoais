# Data model — GastosApp

## Tipos de item na tabela GastosApp

### Transação
- PK: USER#<userId>
- SK: TXN#<YYYY-MM>#<uuid>
- GSI1PK: USER#<userId>#<categoria>
- GSI1SK: <YYYY-MM>#<uuid>
- Atributos: valor (long, centavos), categoria, descricao, data, tipo (despesa|receita)

### Resumo mensal (agregado, atualizado via DynamoDB Streams)
- PK: USER#<userId>
- SK: SUMMARY#<YYYY-MM>
- Atributos: totalDespesas, totalReceitas, saldoMes, porCategoria (Map)

### Categoria
- PK: USER#<userId>
- SK: CAT#<nome>
- Atributos: nome, cor, icone, ativo (bool)

## Regras do modelo
- Valor sempre em centavos (long) — sem float ou decimal no banco
- SK de transação inclui o mês para que begins_with funcione sem GSI extra
- SUMMARY atualizado via Lambda trigger no DynamoDB Streams
- userId vem do JWT — nunca do body

## Backlog (fora do MVP)
- Orçamento por categoria: item BUDGET#YYYY-MM#cat
- Tags em transações: requer GSI2 ou filtro em memória
