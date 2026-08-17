---
name: validate-backend
description: Valida uma task de backend já implementada sem alterar código, executando inspeções, build, testes, banco, health checks e preparando cenários para o Swagger.
argument-hint: "[docs/tasks/task-XX-name.md]"
arguments: [task_file]
disable-model-invocation: true
disallowed-tools: Write, Edit
---

Valide a task de backend `$task_file` sem alterar arquivos.

1. Leia `CLAUDE.md`, a task e os arquivos modificados.
2. Execute `git status --short`, `git diff --check` e revise o diff.
3. Descubra os comandos reais do projeto; não invente scripts.
4. Execute, quando aplicável:
   - restauração de dependências;
   - verificação de formatação;
   - build sem erros;
   - todos os testes relacionados;
   - testes de integração declarados na task;
   - verificação de migrations pendentes;
   - health checks dos serviços e bancos.
5. Inicie somente os containers e serviços necessários se isso for seguro no ambiente local.
6. Forneça uma tabela de testes manuais no Swagger com método, URL, corpo, resposta esperada e verificação no banco/consulta.
7. Não afirme que testes manuais foram realizados pelo usuário.
8. Liste falhas como bloqueadoras e pare sem corrigir código.
9. Não faça staging, commit ou push.

