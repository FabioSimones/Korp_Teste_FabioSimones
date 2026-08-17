# Task 06 - Notas no backend

## Dependências

Tasks 02 e 04 concluídas.

## Agente recomendado

`dotnet-backend`

## Objetivo

Implementar criação e consulta de notas no Faturamento, sem baixa de estoque.

## Escopo permitido

- Entidades `Invoice` e `InvoiceItem`.
- Status `Open` e `Closed`.
- Número sequencial e único gerado no backend.
- Snapshot de código e descrição do produto.
- Validação dos produtos via API do Estoque.
- `POST /api/invoices`.
- `GET /api/invoices`.
- `GET /api/invoices/{id}`.

## Fora do escopo

- Baixa, fechamento, impressão e frontend.

## Testes automatizados

- Um e vários produtos.
- Status inicial e sequência.
- Quantidade inválida, produto repetido e inexistente.
- Consulta, 404 e snapshot.

## Teste manual

- Criar duas notas no Swagger e confirmar números diferentes, status e itens.

## Commit previsto

`feat(billing): add invoice registration and queries`

