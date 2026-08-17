# Arquitetura

## Componentes

```text
Angular
  |-- HTTP --> Inventory.Api --> Inventory Database
  |-- HTTP --> Billing.Api   --> Billing Database
                         |
                         +-- HTTP --> Inventory.Api
```

O Angular usa o Estoque para produtos e o Faturamento para notas. Na impressão, o Angular chama somente o Faturamento; este orquestra a baixa no Estoque.

## Decisões

### Backend

- ASP.NET Core Web API.
- Entity Framework Core.
- PostgreSQL.
- Organização por funcionalidades dentro de cada serviço.
- `ProblemDetails` para erros.
- OpenAPI/Swagger para validação manual.
- Cliente HTTP resiliente entre Faturamento e Estoque.

As versões exatas serão definidas na Task 00 após inspecionar o ambiente e registradas em `docs/technical-details.md`.

### Frontend

- Angular com standalone components.
- Angular Material.
- Reactive Forms e `FormArray`.
- Signals para estado local simples.
- RxJS para requisições e composição assíncrona.
- CSS de impressão com `@media print` e `window.print()`.

### Dados

- Banco separado por microsserviço.
- Nenhuma consulta SQL ou relacionamento entre bancos.
- Faturamento guarda snapshot de código e descrição nos itens da nota.
- Numeração da nota gerada no backend e protegida por unicidade.

## Fluxo de impressão

1. Angular envia `POST /api/invoices/{id}/print`.
2. Faturamento confirma que a nota está aberta.
3. Faturamento cria ou reutiliza um `OperationId`.
4. Faturamento solicita baixa atômica ao Estoque.
5. Estoque valida todos os itens dentro de transação.
6. Estoque registra a operação e desconta os saldos.
7. Faturamento fecha a nota após a confirmação.
8. Angular atualiza a tela e abre a visualização de impressão.

Se o Estoque falhar ou rejeitar a baixa, a nota permanece aberta.

## Erros HTTP

| Situação | Status |
| --- | ---: |
| Entrada inválida | 400 |
| Recurso inexistente | 404 |
| Código duplicado | 409 |
| Saldo insuficiente | 409 |
| Nota fechada | 409 |
| Dependência indisponível | 503 |
| Erro inesperado | 500 |

## Estrutura prevista

```text
src/
├── backend/
│   ├── Inventory.Api/
│   └── Billing.Api/
└── frontend/
    └── invoice-web/
tests/
├── Inventory.Tests/
└── Billing.Tests/
```

