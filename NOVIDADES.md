## Novidades da v0.16

**Novo nome · NEXT STUDIO**
- Tudo que era **RESET** agora é **NEXT**: as categorias viram NEXT BENCH, NEXT OTIMIZA, NEXT MAKER, NEXT JOGOS,
  NEXT DRIVER, NEXT INTEGRAÇÃO, NEXT DESEMPENHO e NEXT FERRAMENTAS; o **RESET EXPLODE** virou **NEXT STUDIO**
  (janela, relatório, diálogos, executável `NextStudio.exe` e pasta de dados em `%AppData%\NextStudio`).

**Ajuste à tela · o app agora cabe em qualquer TV**
- A janela mede a área de trabalho do monitor em que está e nunca abre maior que a tela (antes 840 px de altura
  estouravam a tela em TVs de 1366×768).
- A interface inteira é reescalada em bloco conforme o espaço disponível: em 1366×768 ela opera a 86 %, em 1080p
  a 127 % e preenche a tela; redimensionar ou maximizar reajusta a escala na hora.
- A janela do stress de GPU agora cabe na tela sem perder a resolução de renderização escolhida.

**Nova página · NEXT FERRAMENTAS › Inicialização**
- Lista os programas que abrem junto com o Windows: chaves Run do registro (usuário, todos os usuários, 32 e 64 bits)
  e pastas Inicializar, com descrição, fabricante e aviso quando o arquivo do programa não existe mais.
- Interruptor para ativar/desativar, igual ao Gerenciador de Tarefas: a entrada continua lá e pode ser religada.
- Botão **REMOVER** com confirmação; o programa continua instalado e tudo pode ser desfeito em **Restaurar removidos**.

**Nova página · NEXT FERRAMENTAS › Instalar apps**
- Os essenciais depois de formatar o PC: Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC.
- Instala pelo winget (instalador de pacotes da Microsoft que já vem no Windows): versão oficial mais recente,
  sem janelas, um app de cada vez, com o resultado de cada um na lista.
- Mostra o que já está instalado; se o winget ainda não existir (PC recém-formatado), um botão abre a Microsoft Store
  para atualizar o "Instalador de Aplicativo".

**Nova categoria · NEXT MAKER** (Arduino, ESP32 e conversores USB-serial)
- **Portas COM**: a limpeza de dispositivos fantasmas e de números COM presos saiu de Otimização e ganhou página própria.
- **Drivers seriais**: diagnóstico das placas com CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino
  (sem driver, código 10, código 43, FTDI falsificado travado em PID 0000), com a orientação de cada caso.
- **REINSTALAR** remove o dispositivo e faz o Windows detectar de novo com o melhor driver.
- Lista as versões de driver guardadas no Windows para cada chip, com **REMOVER** (backup antes): volta para uma versão
  anterior quando a nova não funciona com clones, ou desinstala de vez. **Restaurar removidos** desfaz.
- **Drivers incluídos no app**: CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, oficiais e assinados, instalam sem internet
  (ideal logo depois de formatar), com a placa conectada ou não. Também abre a página oficial de cada fabricante.
- **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que prendem a porta COM e limpar o cache de índices
  quando a IDE trava em "Downloading index".

**Novo benchmark · Ray tracing (GPU)**
- Teste em Direct3D 12 com DXR 1.1 que usa os núcleos de ray tracing (RTX 20+, Radeon RX 6000+, Intel Arc): 4 mil objetos,
  reflexos de reflexos, sombras suaves, oclusão de ambiente e iluminação indireta, com a cena refeita a cada frame.
- Feito para comparar com confiança: aquecimento sem medir, 3 passadas iguais, nota pela mediana e variação entre passadas
  (selo de "resultado confiável" abaixo de 3%); tempo de frame medido pelo relógio da própria GPU.
- Presets Normal, Alta e Extrema em 1080p, 1440p e 4K; histórico comparando só resultados do mesmo preset e resolução.
- Referência: RX 9070, Alta 1440p = 878 pts (87,8 FPS), variação 0,5%.

**Overlay personalizável**
- Na aba Overlay, escolha o que aparece: FPS, frametime, 1% low, gráfico; GPU (uso, temperatura, hotspot, clock do núcleo,
  clock da memória, consumo, tensão, ventoinha, limitador NVIDIA); VRAM (usada, %, temperatura); CPU (uso, núcleo mais
  ocupado, temperatura, clock médio, clock máximo, consumo); RAM (GB, %); nome do jogo e hora. Atalhos SÓ FPS, PADRÃO e TUDO.
- Cada componente vira uma linha só com os itens escolhidos; a escolha fica salva e vale na hora.

**Ponto de restauração**
- **CRIAR NO WINDOWS** grava um ponto de restauração do sistema (Proteção do Sistema). Se a proteção estiver desligada,
  o app liga e tenta de novo; o ponto criado aparece em Configurações › Sistema › Proteção do Sistema › Restaura.
- **SALVAR PONTO** guarda o estado dos ajustes do Windows que o app mexeu, para desfazer tudo com um clique.
- Os pontos ficam salvos em `%AppData%\NextStudio\Restauracoes`, com a data e um resumo do que mudou.

**Alertas em segundo plano**
- Com o app aberto, um aviso aparece no canto da tela quando a CPU ou a GPU passa do limite de temperatura,
  quando a bateria acaba fora da tomada e quando um disco dá alerta de saúde.
- Os limites são ajustáveis na tela e o mesmo aviso só se repete depois de 10 minutos.

**Perfil do PC (exportar/importar)**
- **EXPORTAR PERFIL** (em Sistema) salva um arquivo `.nstudio` com o que o app aprendeu sobre a máquina:
  informações do sistema, benchmarks de CPU e de jogo, perfis de jogo, jogos manuais, valores originais dos ajustes
  do Windows, regras de alerta e tema.
- **IMPORTAR PERFIL** traz tudo de volta: o que já existir no PC é mantido e o que falta entra.
- Serve para levar o perfil para outro PC ou guardar um backup antes de formatar.

**NEXT JOGOS**
- Novo atalho em Extras: **-allow_third_party_software** (CS2), que libera captura de jogo do OBS, ReShade e overlays
  bloqueados pelo modo confiável; o aviso sobre o Fator de Confiança aparece ao passar o mouse.

**Abertura mais rápida**
- A janela aparece em ~0,5 s (antes ~4,6 s). Informações do sistema, sensores e placa de vídeo carregam em paralelo,
  em segundo plano, e cada página é preenchida quando a sua parte fica pronta.
- O aviso "sem sensor da CPU" não aparece mais por engano enquanto o hardware ainda está sendo detectado.

**Correção · Overlay de jogos**
- O overlay agora fica por cima de jogos em janela sem borda que também se marcam como "sempre no topo"
  (ex.: GTA V e Cyberpunk 2077), inclusive depois de Alt+Tab.
- Aparece no monitor onde o jogo está, não só no monitor principal.
