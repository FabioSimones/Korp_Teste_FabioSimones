---
name: validate-frontend
description: Valida uma task Angular já implementada sem alterar código, executando formatação, lint, testes, build e preparando cenários de verificação no navegador.
argument-hint: "[docs/tasks/task-XX-name.md]"
arguments: [task_file]
disable-model-invocation: true
disallowed-tools: Write, Edit
---

Valide a task de frontend `$task_file` sem alterar arquivos.

1. Leia `CLAUDE.md`, a task, `package.json` e os arquivos modificados.
2. Execute `git status --short`, `git diff --check` e revise o diff.
3. Use somente os scripts realmente definidos no projeto.
4. Execute, quando disponíveis:
   - instalação reprodutível de dependências pelo lockfile;
   - verificação de formatação;
   - lint;
   - testes automatizados sem modo interativo;
   - build de produção.
5. Verifique tratamento de loading, vazio, validação, sucesso e erro.
6. Forneça roteiro manual com rota, ação, resultado esperado, acessibilidade e tamanhos de tela relevantes.
7. Não afirme que o usuário realizou a validação manual.
8. Liste falhas como bloqueadoras e pare sem corrigir código.
9. Não faça staging, commit ou push.

