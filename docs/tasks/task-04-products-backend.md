# Task 04 - Produtos no backend

## Dependências

Tasks 01 e 02 concluídas.

## Agente recomendado

`dotnet-backend`

## Objetivo

Implementar cadastro e consulta de produtos no Estoque.

## Escopo permitido

- Entidade, DTOs, validações e migration de Produto.
- `POST /api/products`.
- `GET /api/products`.
- `GET /api/products/{id}`.
- Código obrigatório e único, descrição obrigatória e saldo não negativo.
- LINQ para consulta e projeção.

## Fora do escopo

- Edição, exclusão e baixa.
- Notas e frontend.

## Testes automatizados

- Cadastro válido.
- Campos inválidos.
- Código duplicado.
- Listagem, busca existente e 404.
- Persistência no PostgreSQL.

## Teste manual

- Executar os cenários acima no Swagger e reiniciar a API para comprovar persistência.

## Commit previsto

`feat(inventory): add product registration and queries`

