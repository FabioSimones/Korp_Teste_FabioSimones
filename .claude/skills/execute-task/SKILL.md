---
name: execute-task
description: Executa exatamente uma task do projeto Korp, delegando ao agente adequado, implementando testes e encerrando antes do commit. Usar manualmente para iniciar uma task em docs/tasks.
argument-hint: "[docs/tasks/task-XX-name.md]"
arguments: [task_file]
disable-model-invocation: true
---

Execute exclusivamente a task `$task_file`.

1. Confirme que o argumento aponta para um arquivo dentro de `docs/tasks/`. Se estiver ausente ou não existir, pare e informe.
2. Leia integralmente `CLAUDE.md`, `docs/requirements.md`, `docs/architecture.md`, `docs/progress.md` e a task.
3. Execute `git status --short` e preserve alterações preexistentes não relacionadas.
4. Confirme dependências e tasks anteriores declaradas no arquivo. Se alguma não estiver concluída, pare.
5. Identifique o agente recomendado na task:
   - Backend: usar `dotnet-backend`.
   - Frontend: usar `angular-frontend`.
   - Documentação ou fundação mista: executar na conversa principal com o mesmo rigor.
6. Entregue ao agente o caminho da task e instrua-o a respeitar todos os seus limites.
7. Revise o resultado e exija correção de falhas de build ou testes que pertençam à task.
8. Atualize `docs/progress.md` para `Em validação`; não marque como concluída antes do checkpoint.
9. Apresente o relatório obrigatório definido em `CLAUDE.md`.
10. Não crie commit, não execute push e não inicie outra task.

