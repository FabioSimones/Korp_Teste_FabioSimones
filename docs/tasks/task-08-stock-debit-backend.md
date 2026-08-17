# Task 08 - Baixa transacional e idempotente

## Dependência

Task 04 concluída.

## Agente recomendado

`dotnet-backend`

## Objetivo

Implementar no Estoque a baixa atômica de vários produtos com idempotência.

## Escopo permitido

- Operação e movimentos de estoque.
- `POST /api/stock/debits`.
- Transação única para todos os itens.
- Saldo insuficiente com rollback completo.
- `OperationId` único.
- Repetição idempotente.

## Fora do escopo

- Fechamento de nota.
- Chamada pelo Angular.
- Concorrência simultânea, reservada à Task 12.

## Testes automatizados

- Baixa válida e saldo exato.
- Vários produtos.
- Produto inexistente e quantidade inválida.
- Saldo insuficiente sem efeitos parciais.
- Repetição do mesmo `OperationId` sem nova baixa.

## Teste manual

- Produto 10, baixa 2, saldo 8; repetir a operação e confirmar saldo 8; tentar baixa superior e confirmar rollback.

## Commit previsto

`feat(inventory): add transactional and idempotent stock debit`

