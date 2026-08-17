# Task 09 - Impressão e fechamento no backend

## Dependências

Tasks 06 e 08 concluídas.

## Agente recomendado

`dotnet-backend`

## Objetivo

Orquestrar no Faturamento a baixa e o fechamento da nota.

## Escopo permitido

- `POST /api/invoices/{id}/print`.
- Validar nota aberta.
- Criar ou reutilizar `OperationId`.
- Chamar a baixa do Estoque.
- Fechar e preencher `ClosedAt` somente após confirmação.
- Retornar os dados necessários para a impressão.

## Fora do escopo

- Resiliência avançada da Task 11.
- Interface Angular.

## Testes automatizados

- Sucesso, fechamento e saldo reduzido.
- Nota fechada e nota inexistente.
- Saldo insuficiente mantém nota aberta.
- Repetição não duplica a baixa.

## Teste manual

- Criar produto e nota, imprimir pelo Swagger, conferir status/saldo e bloquear nova impressão.

## Commit previsto

`feat(billing): add invoice printing and closing workflow`

