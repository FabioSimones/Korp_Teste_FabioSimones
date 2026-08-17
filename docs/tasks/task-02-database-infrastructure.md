# Task 02 - Bancos e Docker Compose

## Dependência

Task 01 concluída.

## Agente recomendado

`dotnet-backend`

## Objetivo

Configurar persistência PostgreSQL separada para os dois serviços.

## Escopo permitido

- Docker Compose.
- Volumes persistentes e health checks.
- `InventoryDbContext` e `BillingDbContext` sem entidades de negócio.
- Connection strings por variáveis/configuração local.
- Infraestrutura de migrations.

## Fora do escopo

- Entidades e endpoints de produto ou nota.
- Banco compartilhado ou consulta cruzada.

## Testes automatizados

- Conectividade de cada serviço com seu banco quando a suíte suportar integração.

## Teste manual

- Subir containers.
- Validar health checks.
- Reiniciar e confirmar volumes.

## Commit previsto

`chore(database): configure persistent PostgreSQL databases`

