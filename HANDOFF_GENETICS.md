# Handoff: rework de genética Wega + Trauma

## Escopo e confiabilidade

Este documento transfere o contexto da sessão Codex `01a0c54c-fb10-7751-a6ba-270e0d0f2b7d`, cujo histórico de 229 MB foi lido em streaming. Priorizei mensagens do usuário, decisões, resumos finais e resultados de comandos. O histórico também contém trabalho geral do servidor; este handoff resume genética e dependências diretas.

Os resultados de build abaixo são os relatados no histórico, não reproduzidos nesta tarefa. O estado atual foi comparado por leitura do diff e dos arquivos relevantes. Nenhum código foi alterado.

## Objetivo original

Rework da genética no servidor Goob/Dumont, combinando os genes do Corvax Wega com as mecânicas de genética do Trauma Station. A interface central deveria continuar baseada no modificador de DNA Wega; a enumeração hexadecimal seria substituída por blocos numéricos cobrindo genes Wega e Trauma. Para genes equivalentes, usar o efeito/mecânica de Trauma. O gene final da lista fica reservado à troca de espécie, como no Wega; o usuário destacou especificamente o gene 55.

O sequenciamento de enzimas estruturais (EE) deveria ser dividido em grupos de cinco, da esquerda para a direita. Ao lado de cada gene, um botão `???` abre um puzzle no estilo Trauma. Ao acertar, o gene é descoberto/liberado e seus pontos de pesquisa são concedidos à ciência. A troca de espécie continua usando o fluxo Wega.

## Decisões e requisitos acumulados

### Arquitetura e catálogo

- Reutilizar o layout e o fluxo do modificador Wega; incorporar puzzles, descoberta, combinação/receitas e mutações Trauma sem manter catálogos concorrentes.
- Preferir o poder Trauma quando dois genes têm função equivalente. Exceção posterior explícita: substituir o Hulk Trauma pelos genes Hulk e Zilla do Wega; combinar Hulk ou Zilla com Desajeitado libera Ork.
- Manter enumeração determinística, incluir os dois catálogos, aleatorizar estados a cada rodada conforme o pedido original e garantir que o gene de espécie seja sempre o último, inclusive após combinações ou dados antigos.
- Descoberta por sequenciamento concede pesquisa e revela genes ocultos. Genes de receita não aparecem antes de a receita ser descoberta.
- Analgesia Congênita é doença/deficiência e deve reduzir instabilidade (custo negativo), conforme esclarecimento do usuário.

### UI e aparência

- Sliders R/G/B horizontais por característica (cabelo, olhos, pele etc.), todos com cor dinâmica derivada dos valores atuais e atualizados imediatamente juntos. Ao analisar uma entidade, carregar os valores reais dela.
- Altura e peso vêm dos valores reais; cada um tem um único botão de sequenciamento. O sexo inicial acompanha o alvo analisado.
- O usuário pediu repetidamente para preservar o estilo dos botões EE. Texto e `???` ficam recortados dentro do botão; margem de segurança variou conforme iterações (4–6 px, depois 8–10 px, e correções posteriores com 9 px ou subpixel). Repouso centralizado; nomes curtos e `???` não animam; nomes longos só rolam suavemente no hover e voltam imediatamente ao repouso ao sair. Não alterar fonte, cores, bordas, formato, espaçamento ou dimensões, salvo o pedido pontual de reduzir em 5 px a largura. Deve funcionar para genes atuais e futuros, sem lista fixa.

### Analisador portátil

- Reaproveitar janela/estado Wega em modo de leitura. Permitir ler DNA e enzimas, salvar EU, EE ou EU+EE em buffers e importar/exportar dados por disco. Não editar, ativar, sequenciar, combinar, rejuvenescer ou injetar no alvo.
- A aba EE continua consultável com `???`/lacuna, mas não permite resolver sequência. EU permanece visível. Manter prévia do alvo, layout/títulos identificáveis e ocultar apenas funções proibidas.
- Scan por clique deve usar DoAfter de 0,8 s, exigir mão livre/ação contínua e interromper por movimento/dano. Abrir UI e só então enviar o estado atualizado.
- Aceitar DNA da entidade, registros forenses e amostras em soluções/reagentes quando disponíveis.

### Genes e bugs de jogabilidade

- Corrigir Olfato, Visão Laser e Cuspe Ácido: habilidade e efeito devem funcionar. Olfato usa rastros visuais legíveis, tem toggle real e desligar a visualização não apaga rastros ativos; só rastreia alvos válidos no mapa e mantém efeito local ao rastreador.
- Laser e ácido devem ter armas/provedores de munição separados para não consumirem o mesmo evento. Foram pedidos também efeitos/sons corretos, incluindo luz verde para Radioatividade e áudio dos olhos laser.
- Pedidos adicionais: genes Hulk/Zilla/Ork; Super Telecinese com tether azul e mão esquerda adicional, restrita a entidades vivas; Megacefalia escala/restaura camadas visuais; Bioscotocronia mantém escurecimento quando outras luzes se sobrepõem.
- Clonagem deve preservar DNA/enzimas únicas. Genes que mudam nome alteram apenas nome visível, sem mudar identidade/UID interno.

## Implementações relatadas no histórico

Os resumos finais da sessão antiga relatam como implementados em vários commits (alguns foram retrabalhados depois):

- Indexação numérica Wega, sequenciamento/puzzles, descoberta, genes ocultos `???`, desbloqueio e pesquisa.
- Receitas e ordenação do gene de espécie; correções de Hulk/Zilla/Ork.
- Sliders RGB com valores reais, controles de aparência/sexo/altura/peso e ajustes de UI.
- Correções para Olfato, Laser, Ácido, Radioatividade, Bioscotocronia, Super Telecinese, Megacefalia e outros genes.
- Preservação de DNA/enzimas em clonagem e separação entre nome visível e identidade interna.
- Analisador portátil read-only reutilizando a janela normal, buffers/disco, DoAfter e leitura de amostras. Foram necessárias várias correções na ordem de atualização/abas e serialização.
- Regras compartilhadas de instabilidade (`GeneticsInstabilityRules`) usadas por Wega e `MutationSystem` Trauma.

São relatos históricos, não garantia de que cada requisito está completo no checkout atual.

## Arquivos e áreas alterados

O trabalho atravessou muitos commits. Principais pontos de entrada e famílias de arquivos afetadas:

- **Wega shared:** `Content.Shared/_Wega/Genetics/GeneticSequence.cs`, `StructuralEnzymesPrototype.cs`, `StructuralEnzymesIndexer.cs`, `DnaModifier.cs`, `DnaModifierComponent.cs`, `UniqueIdentifiersData.cs`, `UniqueIdentifiersPrototype.cs`, componentes em `Content.Shared/_Wega/Genetics/Components/**` e eventos em `Events/**`. Motivos: representação/ordenação dos genes, estado, catálogo e dados compartilhados de DNA/aparência.
- **Wega server:** `Content.Server/_Wega/Genetics/Systems/DnaModifierSystem.cs` e partes `.Genes.cs`, `.Traits.cs`, `.Map.cs`, `.Clone.cs`; `DnaModifierConsoleSystem.cs` e `.Sequencing.cs`; `PortableGeneticAnalyzerSystem.cs`; comandos em `Commands/**`; sistemas dos genes em `Basic/**`, `Intermediate/**`, `Minor/**`, `Disease/**`. Motivos: ativação/remoção, espécie, receitas, descoberta, ciência, instabilidade, clonagem e analisador.
- **Wega client:** `Content.Client/_Wega/Genetics/Ui/DnaModifierWindow.xaml`, `.xaml.cs`, `.Sequencing.cs`, painéis/controladores; `MegacephalySystem.cs`; sistemas/overlays de doenças. Motivos: tabs, puzzles, botões EE, aparência, sliders, genes cliente e modo portátil.
- **Trauma shared/client:** `Content.Trauma.Shared/Genetics/**` — especialmente `Mutations/MutationSystem.cs`, `MutatableComponent.cs`, console/scanner, enzimas únicas e habilidades — e `Content.Trauma.Client/Genetics/**` (sequenciador, puzzle, console e scanner). Motivos: compartilhar regras de instabilidade e integrar/reutilizar mecânicas Trauma.
- **Dados/build:** protótipos em `Resources/Prototypes/**`, locales em `Resources/Locale/**`, `Content.Client/Content.Client.csproj`, `Content.Server/Content.Server.csproj`, `SpaceStation14.slnx` e scripts `Scripts/bat/**`. Motivos: registrar genes/receitas/localização, dependências e facilitar builds.

A lista acima identifica áreas e arquivos centrais, não cada arquivo de cada commit histórico. Não use isso para sobrescrever o estado local: existem alterações não commitadas e um patch de referência com hunks sobrepostos.

## Builds, testes e erros do histórico

- Builds Windows via `Scripts/bat/buildAllRelease.bat`, builds focados de `Content.Server`/`Content.Client` e testes manuais em servidor/mapa sandbox foram relatados.
- Em várias iterações, compilação terminou com **0 erros C#/RA**, mas MSBuild retornou falha devido a cinco avisos de vulnerabilidade NU1902/NU1903 do ImageSharp. Isso não equivale a erro de compilação.
- Erros de código encontrados e depois relatados como corrigidos: permissões de escrita em `BatteryComponent` (laser); `Vector2` ausente (olfato); `Control`/namespace de UI; tipos/`using` e comparação de `ProtoId` no Hulk/laser; padrões `is` inválidos para `ProtoId`; resolução de `System.Numerics` em Megacefalia; API de mãos protegidas; `System.Linq` ausente; atributos/registro de componentes e eventos de rede; inscrição duplicada de evento de aparência e protótipo de rastro duplicado.
- Problemas de runtime registrados: cliente voltava ao menu ao abrir portátil; PVS falhava ao serializar um DoAfter customizado não registrado; assert durante shutdown ao aplicar ativador geral de genes num macaco. O fluxo atual do portátil tenta evitar o evento customizado reutilizando `DnaInjectorDoAfterEvent`. Parte dos logs posteriores foi atribuída a binários/branches antigos; validar cliente e servidor com a mesma build.
- Alguns builds nem chegaram ao código por diretório incorreto, ausência de `project.assets.json` após limpeza NuGet, falta de espaço ou log antigo misturado. Esses resultados não provam erro de código.
- **Fim do histórico:** o último estado visível diz que o build de servidor ainda estava rodando/grvando log para validar dois acessos redundantes `.Solution` no analisador. Não há resultado final no JSONL; não afirmar que passou.
- Nenhum build/teste foi executado para este handoff.

## Estado atual do repositório

Estado observado nesta sessão, sem editar código:

- Branch local `fix/genetic-build` (singular `genetic`); upstream aparece `origin/fix/genetic-build [gone]`. O usuário confirmou esse nome como correto em mensagens recentes. O histórico antigo tem confusão de branch criada por tentativas de usar `fix-genetics-build`, `fix/genetics-build` e outros nomes. Não renomear/publicar fora de um pedido correspondente.
- Alterações rastreadas: `Content.Server/_Wega/Genetics/Systems/PortableGeneticAnalyzerSystem.cs` (dois usos de `solution.Contents`, não `.Solution.Contents`); `Content.Trauma.Shared/Genetics/Mutations/MutationSystem.cs` (regras compartilhadas de instabilidade); remoção de `Resources/Locale/en-US/entity-systems/bin/bin-system.ftl`; ponteiro do submódulo `RobustToolbox` alterado.
- Não rastreados: `Scripts/bat/buildAndShowErrors.bat`, `geneticanalyzer.rsi/` e `genetics-fixes.patch`. O patch de referência contém 19 arquivos, 219 inserções/49 remoções segundo `git apply --stat`; inclui referências de projeto, comandos admin e UI. Não foi aplicado e sobrepõe mudanças locais, inclusive a remoção de locale.
- `Content.Shared/_Wega/Genetics/GeneticsInstabilityRules.cs` define aviso 20, estágios após 35 e 65 e melting 100. Trauma `MutationSystem` e Wega `DnaModifierSystem` consultam as regras. O total Wega deriva custos de EE; Trauma acumula instabilidade de mutações.
- `Content.Trauma.Shared` e `Content.Trauma.Client` existem, mas os csproj observados de `Content.Server` e `Content.Client` não declaram referências a eles. `Content.Trauma.Shared.csproj` referencia `Content.Shared`. O patch propõe incluir refs server/client; verificar grafo e necessidade antes de alterar.
- `git diff --check` passou na inspeção anterior. Não foi feito build nesta sessão; erros atuais de compilação ainda não estão determinados.

## Problemas conhecidos e próximos passos

1. Revisar `git status`, diffs e `genetics-fixes.patch` hunk por hunk; não aplicar o patch inteiro nem sobrescrever as quatro mudanças rastreadas ou os itens não rastreados.
2. Confirmar grafo de referências server/client para Trauma e executar build limpo focado, separando erros C#/RA dos avisos NU1902/NU1903.
3. Verificar implementação real de enumeração combinada, randomização por rodada, sequência em grupos de cinco, desbloqueio/pesquisa e gene de espécie sempre final — sincronizados entre UI, servidor, buffers e receitas.
4. Revisar as regras compartilhadas: `Add` soma sem clamp; `Stage` usa comparações estritas `>`; o popup compartilhado removeu “cold”/“cells burning” do mapeamento Trauma anterior. Confirmar se são decisões intencionais. Checar também coerência de custo entre genes EE e mutações Trauma.
5. Testar runtime do portátil: scan/DoAfter, primeira atualização de estado, abas, salvar EU/EE/EU+EE, disco, amostras forenses/soluções e garantia de que o alvo nunca é alterado.
6. Revalidar em jogo falhas que voltaram em iterações: receitas antes da descoberta, texto EE fora do botão, Olfato/Laser/Ácido, troca de espécie repetida/entidade original, clone com identificador novo e desconexão por PVS/binário incompatível.
7. Confirmar se o ponteiro de `RobustToolbox` é intencional antes de qualquer commit; a sessão antiga registrou builds que alteravam arquivos internos do submódulo.

## Preferências e instruções do usuário

- Trabalhar na branch correta `fix/genetic-build`; não criar/trocar branches por engano. O usuário corrigiu explicitamente nomes parecidos.
- Investigar antes de corrigir e levar o tempo necessário para bugs de genes e crashes. Separar erros reais de avisos e comunicar o resultado real do build.
- Preservar alterações existentes, limitar cada mudança ao escopo pedido e não redesenhar UI sem solicitação. Para botões EE, seguir as regras detalhadas acima.
- Não declarar tarefas planejadas como concluídas. O usuário quer saber quais genes funcionam, seus custos de instabilidade e seus efeitos.
- Não reinstalar o projeto nem descartar trabalho local; evitar logs antigos e artefatos/binários desatualizados ao validar.
- Embora commits/publicações tenham sido pedidos em tarefas antigas, isso não autoriza publicar em toda tarefa futura. O pedido atual autorizava apenas criar este arquivo.

## Próximo passo recomendado

Começar por status e revisão do patch sem aplicá-lo; depois fechar o grafo Trauma server/client e obter build limpo dos projetos envolvidos. Continuar em mudanças pequenas, validar sequência/espécie e testar o analisador portátil em runtime.
