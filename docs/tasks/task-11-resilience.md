# Task 11 - Resiliência e falha obrigatória

## Dependência

Task 09 concluída.

## Agente recomendado

`dotnet-backend`

## Objetivo

Implementar e demonstrar recuperação quando o Estoque está indisponível.

## Escopo permitido

- Timeout curto.
- Retry limitado com espera progressiva.
- Circuit breaker.
- Erro 503 padronizado.
- Correlation ID e logs.
- Manutenção da nota aberta.
- Reutilização segura do `OperationId`.

## Fora do escopo

- Broker de mensagens, service mesh ou orquestrador externo.

## Testes automatizados

- Indisponibilidade, timeout e erro temporário seguido de recuperação.
- Circuit breaker.
- Nota aberta após falha.
- Ausência de baixa duplicada após nova tentativa.

## Teste manual

- Parar Estoque, tentar imprimir, validar erro e nota aberta; reiniciar, repetir e confirmar uma única baixa.

## Commit previsto

`feat(resilience): handle inventory service failures`

