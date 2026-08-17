# Pacote de trabalho para Claude Code

Este pacote contém instruções, agentes, skills e tasks para desenvolver o desafio Korp em entregas pequenas e verificáveis.

## Instalação

1. Extraia o conteúdo deste pacote na raiz do repositório `Korp_Teste_FabioSimones`.
2. Confirme que `CLAUDE.md`, `.claude/` e `docs/` ficaram diretamente na raiz.
3. Abra o VS Code nessa raiz e inicie uma nova sessão do Claude Code.
4. Use `/context` para confirmar que `CLAUDE.md` foi carregado.
5. Use `/skills` para confirmar as quatro skills do projeto.

Não copie a pasta externa `korp-claude-code-workflow`; copie o conteúdo dela.

## Primeiro comando

```text
/execute-task docs/tasks/task-00-project-foundation.md
```

Depois da implementação, valide conforme o tipo da task:

```text
/validate-backend docs/tasks/task-04-products-backend.md
```

ou:

```text
/validate-frontend docs/tasks/task-05-products-frontend.md
```

Após os testes automatizados, a revisão e sua aprovação manual:

```text
/create-checkpoint "feat(inventory): add product registration and queries" aprovado
```

## Regras importantes

- Execute uma task por vez.
- Não permita que o Claude avance para a próxima task automaticamente.
- Faça a validação manual indicada antes de criar o checkpoint.
- Não use agentes em paralelo no mesmo working tree.
- Não publique segredos, arquivos `.env` ou senhas no Git.
- Revise as versões detectadas de .NET, Node e Angular na Task 00 antes de iniciar o código.

## Agentes disponíveis

- `@agent-dotnet-backend`: backend ASP.NET Core, EF Core e testes.
- `@agent-angular-frontend`: Angular, RxJS, formulários e testes.
- `@agent-quality-reviewer`: revisão somente leitura antes do commit.

## Skills disponíveis

- `/execute-task`: executa somente a task informada.
- `/validate-backend`: valida build, testes, banco, health checks e Swagger.
- `/validate-frontend`: valida formatação, lint, testes, build e cenários manuais.
- `/create-checkpoint`: revisa e cria o commit após aprovação explícita.

