using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with labels for website.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedLabelsAsync(ApplicationDbContext dbContext)
    {
        // Seed Labels data
        if (!await dbContext.Labels.AnyAsync())
        {
            var labels = new List<Label>();

            // Homepage label
            var aboutUsLabel = Label.Create(
                "about-us",
                "Sobre Nós",
                "A Real Tuna Universitária de Bragança (RTUB) é a tuna masculina do Instituto Politécnico de Bragança. Fundada a 1 de dezembro de 1991, a RTUB prima por enaltecer o espírito académico vivido nesta academia através da música que produz.\r\n\r\nCom o preto e o roxo como cores estandarte, o grupo é um pilar da vida estudantil, levando o nome de Bragança e do IPB a palcos de todo o país e além-fronteiras. Mais do que um grupo musical, a RTUB é uma escola de vida e um ponto de encontro de gerações.\r\n\r\nSomos conhecidos por organizar anualmente o prestigiado FITAB – Festival Internacional de Tunas Académicas de Bragança, um evento que celebra a música e a camaradagem entre tunas de várias origens. Honrando o passado enquanto olhamos para o futuro, continuamos a celebrar a boémia, a amizade e a tradição."
            );
            aboutUsLabel.Activate();
            labels.Add(aboutUsLabel);

            // Join Us page labels
            var joinUsTitle = Label.Create("join_us_title", "JUNTA-TE A NÓS", "JUNTA-TE A NÓS");
            joinUsTitle.Activate();
            labels.Add(joinUsTitle);

            var joinUsSubtitle = Label.Create("join_us_subtitle", "Queres fazer parte da família?", "Queres fazer parte da família?");
            joinUsSubtitle.Activate();
            labels.Add(joinUsSubtitle);

            var joinUsIntro = Label.Create("join_us_intro", "Introdução", "A RTUB está sempre de portas abertas para novos elementos que queiram viver a experiência única da vida tunante!");
            joinUsIntro.Activate();
            labels.Add(joinUsIntro);

            var joinUsScheduleTitle = Label.Create("join_us_schedule_title", "Quando", "📅 Quando:");
            joinUsScheduleTitle.Activate();
            labels.Add(joinUsScheduleTitle);

            var joinUsScheduleContent = Label.Create("join_us_schedule_content", "Horário", "Terças e Quintas-feiras\n21h00 - 00h00");
            joinUsScheduleContent.Activate();
            labels.Add(joinUsScheduleContent);

            var joinUsLocationTitle = Label.Create("join_us_location_title", "Onde", "📍 Onde:");
            joinUsLocationTitle.Activate();
            labels.Add(joinUsLocationTitle);

            var joinUsLocationContent = Label.Create("join_us_location_content", "Localização", "Quinta de Santa Apolónia, Freguesia da Sé\nInstituto Politécnico de Bragança");
            joinUsLocationContent.Activate();
            labels.Add(joinUsLocationContent);

            var joinUsActivitiesTitle = Label.Create("join_us_activities_title", "O que fazemos", "🎵 O que fazemos:");
            joinUsActivitiesTitle.Activate();
            labels.Add(joinUsActivitiesTitle);

            var joinUsActivitiesContent = Label.Create("join_us_activities_content", "Atividades", "Ensaios musicais (guitarra, bandolim, cavaquinho, voz)\nConvívio e integração\nPreparação de atuações\nAprendizagem de repertório tradicional e original");
            joinUsActivitiesContent.Activate();
            labels.Add(joinUsActivitiesContent);

            var joinUsNoExperience = Label.Create("join_us_no_experience", "Sem experiência necessária", "Não precisas de saber tocar! Ensina-se tudo. O que conta é a vontade de aprender, o espírito de grupo e a paixão pela música e tradição académica.");
            joinUsNoExperience.Activate();
            labels.Add(joinUsNoExperience);

            var joinUsHowToTitle = Label.Create("join_us_how_to_title", "Como aderir", "Como aderir:");
            joinUsHowToTitle.Activate();
            labels.Add(joinUsHowToTitle);

            var joinUsHowToContent = Label.Create("join_us_how_to_content", "Como aderir conteúdo", "Aparece num dos nossos ensaios ou contacta-nos através das redes sociais. O primeiro passo é simples: vem conhecer-nos!");
            joinUsHowToContent.Activate();
            labels.Add(joinUsHowToContent);

            // History page labels
            var historyTitle = Label.Create("history_title", "A NOSSA HISTÓRIA", "A NOSSA HISTÓRIA");
            historyTitle.Activate();
            labels.Add(historyTitle);

            var historySubtitle = Label.Create("history_subtitle", "Das origens aos dias de hoje", "Das origens aos dias de hoje");
            historySubtitle.Activate();
            labels.Add(historySubtitle);

            var historyIntro = Label.Create("history_intro", "Introdução", "A Real Tuna Universitária de Bragança - Boémios e Trovadores nasceu a 1 de dezembro de 1991, tornando-se a tuna masculina do Instituto Politécnico de Bragança e um dos pilares da vida académica brigantina.");
            historyIntro.Activate();
            labels.Add(historyIntro);

            var historyMissionTitle = Label.Create("history_mission_title", "Missão e Valores", "Missão e Valores:");
            historyMissionTitle.Activate();
            labels.Add(historyMissionTitle);

            var historyMissionContent = Label.Create("history_mission_content", "Missão conteúdo", "Desde a sua fundação, a RTUB dedica-se a enaltecer o espírito académico através da música, cultivando valores como:\n\n🎭 A tradição académica e tunante\n🤝 A amizade e irmandade\n🎶 A excelência musical\n🍷 A boémia e alegria de viver\n🏰 O amor por Bragança e pelo IPB");
            historyMissionContent.Activate();
            labels.Add(historyMissionContent);

            var historyIdentityTitle = Label.Create("history_identity_title", "Identidade Visual", "Identidade Visual:");
            historyIdentityTitle.Activate();
            labels.Add(historyIdentityTitle);

            var historyIdentityContent = Label.Create("history_identity_content", "Identidade conteúdo", "As nossas cores preto e roxo são mais que cores - são símbolo de elegância, tradição e união. O nosso símbolo exibe o icónico Castelo de Bragança, marcando a nossa ligação indissociável à cidade.");
            historyIdentityContent.Activate();
            labels.Add(historyIdentityContent);

            var historyJourneyTitle = Label.Create("history_journey_title", "Percurso", "Percurso:");
            historyJourneyTitle.Activate();
            labels.Add(historyJourneyTitle);

            var historyJourneyContent = Label.Create("history_journey_content", "Percurso conteúdo", "Ao longo de mais de três décadas, a RTUB tem:\n\nLevado o nome de Bragança e do IPB a palcos por todo o país e além-fronteiras\nCriado um vasto repertório original (presente no nosso Cancioneiro desde 1995)\nOrganizado o prestigiado FITAB - Festival Internacional de Tunas Académicas\nFormado gerações de estudantes na música e na vida");
            historyJourneyContent.Activate();
            labels.Add(historyJourneyContent);

            var historyMoreTitle = Label.Create("history_more_title", "Mais que música", "Mais que música:");
            historyMoreTitle.Activate();
            labels.Add(historyMoreTitle);

            var historyMoreContent = Label.Create("history_more_content", "Mais conteúdo", "A RTUB é uma escola de vida, um ponto de encontro de gerações onde se forjam amizades para toda a vida. Das serenatas às viagens, dos copos aos palcos, vivemos intensamente cada momento da tradição tunante.");
            historyMoreContent.Activate();
            labels.Add(historyMoreContent);

            // Hierarchy page labels
            var hierarchyTitle = Label.Create("hierarchy_title", "HIERARQUIA", "HIERARQUIA");
            hierarchyTitle.Activate();
            labels.Add(hierarchyTitle);

            var hierarchySubtitle = Label.Create("hierarchy_subtitle", "A estrutura da família RTUB", "A estrutura da família RTUB");
            hierarchySubtitle.Activate();
            labels.Add(hierarchySubtitle);

            var hierarchyIntro = Label.Create("hierarchy_intro", "Introdução", "A vida na tuna segue uma progressão natural que acompanha o crescimento pessoal e musical de cada elemento:");
            hierarchyIntro.Activate();
            labels.Add(hierarchyIntro);

            var hierarchyLeitaoTitle = Label.Create("hierarchy_leitao_title", "Leitão", "🐷 LEITÃO");
            hierarchyLeitaoTitle.Activate();
            labels.Add(hierarchyLeitaoTitle);

            var hierarchyLeitaoWho = Label.Create("hierarchy_leitao_who", "Quem são", "Quem são: Candidatos que ainda não foram oficialmente admitidos");
            hierarchyLeitaoWho.Activate();
            labels.Add(hierarchyLeitaoWho);

            var hierarchyLeitaoProcess = Label.Create("hierarchy_leitao_process", "Processo", "Processo: Período de observação e integração inicial");
            hierarchyLeitaoProcess.Activate();
            labels.Add(hierarchyLeitaoProcess);

            var hierarchyLeitaoDuration = Label.Create("hierarchy_leitao_duration", "Duração", "Duração: Até à aprovação pelo Conselho de Veteranos");
            hierarchyLeitaoDuration.Activate();
            labels.Add(hierarchyLeitaoDuration);

            var hierarchyCaloiroTitle = Label.Create("hierarchy_caloiro_title", "Caloiro", "🌱 CALOIRO");
            hierarchyCaloiroTitle.Activate();
            labels.Add(hierarchyCaloiroTitle);

            var hierarchyCaloiroWho = Label.Create("hierarchy_caloiro_who", "Quem são", "Quem são: Novos membros oficialmente admitidos na RTUB");
            hierarchyCaloiroWho.Activate();
            labels.Add(hierarchyCaloiroWho);

            var hierarchyCaloiroCharacteristics = Label.Create("hierarchy_caloiro_characteristics", "Características", "Características:\n• Aprendem o repertório e os instrumentos\n• Integram-se na cultura tunante\n• Têm direito a 1 voto em Assembleia Geral");
            hierarchyCaloiroCharacteristics.Activate();
            labels.Add(hierarchyCaloiroCharacteristics);

            var hierarchyCaloiroDuration = Label.Create("hierarchy_caloiro_duration", "Duração", "Duração: Mínimo de 1 ano");
            hierarchyCaloiroDuration.Activate();
            labels.Add(hierarchyCaloiroDuration);

            var hierarchyCaloiroTransition = Label.Create("hierarchy_caloiro_transition", "Transição", "Transição: Aprovação do Conselho de Veteranos + cerimónia de admissão");
            hierarchyCaloiroTransition.Activate();
            labels.Add(hierarchyCaloiroTransition);

            var hierarchyTunoTitle = Label.Create("hierarchy_tuno_title", "Tuno", "🎸 TUNO");
            hierarchyTunoTitle.Activate();
            labels.Add(hierarchyTunoTitle);

            var hierarchyTunoWho = Label.Create("hierarchy_tuno_who", "Quem são", "Quem são: Membros com experiência e conhecimento consolidado");
            hierarchyTunoWho.Activate();
            labels.Add(hierarchyTunoWho);

            var hierarchyTunoCharacteristics = Label.Create("hierarchy_tuno_characteristics", "Características", "Características:\n• Domínio do repertório e instrumentos\n• Papel ativo nas decisões da tuna\n• Têm direito a 3 votos em Assembleia Geral");
            hierarchyTunoCharacteristics.Activate();
            labels.Add(hierarchyTunoCharacteristics);

            var hierarchyTunoDuration = Label.Create("hierarchy_tuno_duration", "Duração", "Duração: Mínimo de 2 anos");
            hierarchyTunoDuration.Activate();
            labels.Add(hierarchyTunoDuration);

            var hierarchyTunoTransition = Label.Create("hierarchy_tuno_transition", "Transição", "Transição: Automática após 2 anos como Tuno → VETERANO");
            hierarchyTunoTransition.Activate();
            labels.Add(hierarchyTunoTransition);

            var hierarchyVeteranoTitle = Label.Create("hierarchy_veterano_title", "Veterano", "👑 VETERANO");
            hierarchyVeteranoTitle.Activate();
            labels.Add(hierarchyVeteranoTitle);

            var hierarchyVeteranoWho = Label.Create("hierarchy_veterano_who", "Quem são", "Quem são: A experiência e sabedoria da RTUB");
            hierarchyVeteranoWho.Activate();
            labels.Add(hierarchyVeteranoWho);

            var hierarchyVeteranoCharacteristics = Label.Create("hierarchy_veterano_characteristics", "Características", "Características:\n• Membros com mais de 2 anos como Tuno\n• Formam o Conselho de Veteranos\n• Decidem admissões e transições de categoria\n• Têm direito a 5 votos em Assembleia Geral");
            hierarchyVeteranoCharacteristics.Activate();
            labels.Add(hierarchyVeteranoCharacteristics);

            var hierarchyVeteranoSpecial = Label.Create("hierarchy_veterano_special", "Menção especial", "Menção especial: Com 4+ anos como Veterano no ativo = TUNOSSAURO 🦕");
            hierarchyVeteranoSpecial.Activate();
            labels.Add(hierarchyVeteranoSpecial);

            var hierarchySpecialTitle = Label.Create("hierarchy_special_title", "Categorias Especiais", "🎖️ CATEGORIAS ESPECIAIS");
            hierarchySpecialTitle.Activate();
            labels.Add(hierarchySpecialTitle);

            var hierarchySpecialReformed = Label.Create("hierarchy_special_reformed", "Reformados", "Reformados: Membros afastados temporariamente (>6 meses) por motivos profissionais/pessoais. Podem retornar após 3 meses de atividade.");
            hierarchySpecialReformed.Activate();
            labels.Add(hierarchySpecialReformed);

            var hierarchySpecialHonorary = Label.Create("hierarchy_special_honorary", "Tunos Honorários", "Tunos Honorários: Distinção atribuída pela Assembleia Geral a indivíduos ou instituições por atos notáveis em benefício da RTUB.");
            hierarchySpecialHonorary.Activate();
            labels.Add(hierarchySpecialHonorary);

            // Hierarchy photo card labels
            var hierarchyLeitaoCardTitle = Label.Create("hierarchy_leitao_card_title", "Candidato", "Candidato");
            hierarchyLeitaoCardTitle.Activate();
            labels.Add(hierarchyLeitaoCardTitle);

            var hierarchyLeitaoCardDesc = Label.Create("hierarchy_leitao_card_desc", "Leitão Description", "Traje casual, sem vestes oficiais. Período de observação e integração inicial na tuna.");
            hierarchyLeitaoCardDesc.Activate();
            labels.Add(hierarchyLeitaoCardDesc);

            var hierarchyCaloiroCardTitle = Label.Create("hierarchy_caloiro_card_title", "Novo Membro", "Novo Membro");
            hierarchyCaloiroCardTitle.Activate();
            labels.Add(hierarchyCaloiroCardTitle);

            var hierarchyCaloiroCardDesc = Label.Create("hierarchy_caloiro_card_desc", "Caloiro Description", "Capa preta e roxa sem fitas. Primeiros passos na tradição tunante com traje parcial.");
            hierarchyCaloiroCardDesc.Activate();
            labels.Add(hierarchyCaloiroCardDesc);

            var hierarchyTunoCardTitle = Label.Create("hierarchy_tuno_card_title", "Membro Experiente", "Membro Experiente");
            hierarchyTunoCardTitle.Activate();
            labels.Add(hierarchyTunoCardTitle);

            var hierarchyTunoCardDesc = Label.Create("hierarchy_tuno_card_desc", "Tuno Description", "Traje completo com capa, fitas e insígnias. Representa a experiência e conhecimento consolidado.");
            hierarchyTunoCardDesc.Activate();
            labels.Add(hierarchyTunoCardDesc);

            var hierarchyMagisterCardTitle = Label.Create("hierarchy_magister_card_title", "Presidente", "Presidente");
            hierarchyMagisterCardTitle.Activate();
            labels.Add(hierarchyMagisterCardTitle);

            var hierarchyMagisterCardDesc = Label.Create("hierarchy_magister_card_desc", "Magister Description", "Traje distintivo com cordões roxos. Máximo representante e líder da RTUB.");
            hierarchyMagisterCardDesc.Activate();
            labels.Add(hierarchyMagisterCardDesc);

            // Governing Bodies page labels
            var bodiesTitle = Label.Create("bodies_title", "ÓRGÃOS SOCIAIS", "ÓRGÃOS SOCIAIS");
            bodiesTitle.Activate();
            labels.Add(bodiesTitle);

            var bodiesSubtitle = Label.Create("bodies_subtitle", "A direção e organização", "A direção e organização");
            bodiesSubtitle.Activate();
            labels.Add(bodiesSubtitle);

            var bodiesIntro = Label.Create("bodies_intro", "Introdução", "A RTUB rege-se por órgãos sociais eleitos anualmente pelos seus membros:");
            bodiesIntro.Activate();
            labels.Add(bodiesIntro);

            var bodiesDirectionTitle = Label.Create("bodies_direction_title", "Direção", "DIREÇÃO");
            bodiesDirectionTitle.Activate();
            labels.Add(bodiesDirectionTitle);

            var bodiesDirectionStructure = Label.Create("bodies_direction_structure", "Estrutura Direção", "Magister (Presidente): Máximo representante da RTUB\nVice-Magister (Vice-Presidente)\nSecretário\n1º Tesoureiro\n2º Tesoureiro");
            bodiesDirectionStructure.Activate();
            labels.Add(bodiesDirectionStructure);

            var bodiesDirectionFunctions = Label.Create("bodies_direction_functions", "Funções Direção", "Funções: Administração, gestão, representação externa, execução do plano de atividades");
            bodiesDirectionFunctions.Activate();
            labels.Add(bodiesDirectionFunctions);

            var bodiesVeteransTitle = Label.Create("bodies_veterans_title", "Conselho de Veteranos", "CONSELHO DE VETERANOS");
            bodiesVeteransTitle.Activate();
            labels.Add(bodiesVeteransTitle);

            var bodiesVeteransStructure = Label.Create("bodies_veterans_structure", "Estrutura Veteranos", "Formado por todos os Veteranos no ativo + Magister + 1 representante dos Tunos\nPresidente: Eleito pelos Veteranos");
            bodiesVeteransStructure.Activate();
            labels.Add(bodiesVeteransStructure);

            var bodiesVeteransFunctions = Label.Create("bodies_veterans_functions", "Funções Veteranos", "Funções: Admissões, transições de categoria, consultoria estratégica");
            bodiesVeteransFunctions.Activate();
            labels.Add(bodiesVeteransFunctions);

            var bodiesAssemblyTitle = Label.Create("bodies_assembly_title", "Assembleia Geral", "ASSEMBLEIA GERAL");
            bodiesAssemblyTitle.Activate();
            labels.Add(bodiesAssemblyTitle);

            var bodiesAssemblyStructure = Label.Create("bodies_assembly_structure", "Estrutura Assembleia", "Mesa: Presidente, 1º Secretário, 2º Secretário\nTodos os membros efetivos participam");
            bodiesAssemblyStructure.Activate();
            labels.Add(bodiesAssemblyStructure);

            var bodiesAssemblyFunctions = Label.Create("bodies_assembly_functions", "Funções Assembleia", "Funções: Órgão máximo de decisão, aprovação de contas, alterações estatutárias");
            bodiesAssemblyFunctions.Activate();
            labels.Add(bodiesAssemblyFunctions);

            var bodiesFiscalTitle = Label.Create("bodies_fiscal_title", "Conselho Fiscal", "CONSELHO FISCAL");
            bodiesFiscalTitle.Activate();
            labels.Add(bodiesFiscalTitle);

            var bodiesFiscalStructure = Label.Create("bodies_fiscal_structure", "Estrutura Fiscal", "Presidente + 2 Relatores");
            bodiesFiscalStructure.Activate();
            labels.Add(bodiesFiscalStructure);

            var bodiesFiscalFunctions = Label.Create("bodies_fiscal_functions", "Funções Fiscal", "Funções: Fiscalização financeira e cumprimento dos estatutos");
            bodiesFiscalFunctions.Activate();
            labels.Add(bodiesFiscalFunctions);

            // FITAB page labels
            var fitabTitle = Label.Create("fitab_title", "FITAB", "FITAB");
            fitabTitle.Activate();
            labels.Add(fitabTitle);

            var fitabSubtitle = Label.Create("fitab_subtitle", "Festival Internacional de Tunas Académicas de Bragança", "Festival Internacional de Tunas Académicas de Bragança");
            fitabSubtitle.Activate();
            labels.Add(fitabSubtitle);

            var fitabIntro = Label.Create("fitab_intro", "Introdução", "O FITAB é o evento anual mais prestigiado organizado pela RTUB, celebrando a música, tradição e confraternização entre tunas de todo o mundo.");
            fitabIntro.Activate();
            labels.Add(fitabIntro);

            var fitabWhatTitle = Label.Create("fitab_what_title", "O que é", "O que é:");
            fitabWhatTitle.Activate();
            labels.Add(fitabWhatTitle);

            var fitabWhatContent = Label.Create("fitab_what_content", "O que é conteúdo", "Um festival que reúne tunas académicas portuguesas e internacionais para dias de música, partilha cultural e convívio numa atmosfera única de camaradagem tunante.");
            fitabWhatContent.Activate();
            labels.Add(fitabWhatContent);

            var fitabFeaturesTitle = Label.Create("fitab_features_title", "Características", "Características:");
            fitabFeaturesTitle.Activate();
            labels.Add(fitabFeaturesTitle);

            var fitabFeaturesContent = Label.Create("fitab_features_content", "Características conteúdo", "🎭 Atuações de múltiplas tunas\n🏆 Promoção da cultura académica\n🌍 Intercâmbio cultural internacional\n🎉 Convívio e festa académica\n🏰 Valorização de Bragança como cidade universitária");
            fitabFeaturesContent.Activate();
            labels.Add(fitabFeaturesContent);

            var fitabWhenTitle = Label.Create("fitab_when_title", "Quando", "Quando:");
            fitabWhenTitle.Activate();
            labels.Add(fitabWhenTitle);

            var fitabWhenContent = Label.Create("fitab_when_content", "Quando conteúdo", "Realiza-se anualmente (consultar calendário específico nas redes sociais)");
            fitabWhenContent.Activate();
            labels.Add(fitabWhenContent);

            var fitabImportanceTitle = Label.Create("fitab_importance_title", "Importância", "Importância:");
            fitabImportanceTitle.Activate();
            labels.Add(fitabImportanceTitle);

            var fitabImportanceContent = Label.Create("fitab_importance_content", "Importância conteúdo", "O FITAB coloca Bragança no mapa dos grandes eventos de tunas em Portugal, atraindo centenas de tunos e espectadores, dinamizando a cidade e reforçando a importância da RTUB no panorama tunante nacional e internacional.");
            fitabImportanceContent.Activate();
            labels.Add(fitabImportanceContent);

            // Requests page labels
            var requestsWhatToExpect = Label.Create("requests_what_to_expect", "O que esperar", "Analisaremos o seu pedido dentro de 48 horas\nUm membro da equipa entrará em contacto para discutir detalhes\nForneceremos um orçamento baseado nos seus requisitos\nReserve cedo para garantir disponibilidade");
            requestsWhatToExpect.Activate();
            labels.Add(requestsWhatToExpect);

            await dbContext.Labels.AddRangeAsync(labels);
            await dbContext.SaveChangesAsync();
        }
    }
}