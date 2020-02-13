Imports System.Collections.ObjectModel
Imports System.Windows
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models

Public Class AgenciaCorreosExpress
    Implements IAgencia

    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Public Sub New()

        NotificationRequest = New InteractionRequest(Of INotification)
        'ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        ListaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "No disponible")
        }
        ListaServicios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(63, "Paq 24"),
            New tipoIdDescripcion(66, "Baleares"),
            New tipoIdDescripcion(69, "Canarias Marítimo"),
            New tipoIdDescripcion(90, "Internacional Estándar"),
            New tipoIdDescripcion(54, "EntregaPlus (entrega+recogida)"),
            New tipoIdDescripcion(92, "Paq Empresa 14")
        }
        ListaHorarios = New ObservableCollection(Of tipoIdDescripcion) From {
            New tipoIdDescripcion(0, "No disponible")
        }

        ListaPaises = rellenarPaises()
    End Sub

    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Visible
        End Get
    End Property

    Public ReadOnly Property retornoSoloCobros As Integer Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' No disponible
        End Get
    End Property

    Public ReadOnly Property servicioSoloCobros As Integer Implements IAgencia.servicioSoloCobros
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property horarioSoloCobros As Integer Implements IAgencia.horarioSoloCobros
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property retornoSinRetorno As Integer Implements IAgencia.retornoSinRetorno
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property retornoObligatorio As Integer Implements IAgencia.retornoObligatorio
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public ReadOnly Property paisDefecto As Integer Implements IAgencia.paisDefecto
        Get
            Return 724 ' España
        End Get
    End Property

    Public ReadOnly Property ListaPaises As ObservableCollection(Of Pais) Implements IAgencia.ListaPaises
    Public ReadOnly Property ListaTiposRetorno As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaTiposRetorno
    Public ReadOnly Property ListaServicios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaServicios
    Public ReadOnly Property ListaHorarios As ObservableCollection(Of tipoIdDescripcion) Implements IAgencia.ListaHorarios
    Public ReadOnly Property ServicioDefecto As Integer Implements IAgencia.ServicioDefecto
        Get
            Return 92 ' Paq Empresa 14
        End Get
    End Property

    Public ReadOnly Property HorarioDefecto As Integer Implements IAgencia.HorarioDefecto
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public Sub calcularPlaza(codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        Throw New NotImplementedException()
    End Sub

    Public Sub llamadaWebService(servicio As IAgenciaService) Implements IAgencia.llamadaWebService
        Throw New NotImplementedException()
    End Sub

    Public Sub imprimirEtiqueta() Implements IAgencia.imprimirEtiqueta
        Throw New NotImplementedException()
    End Sub

    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        Throw New NotImplementedException()
    End Function

    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Throw New NotImplementedException()
    End Function

    Public Function calcularCodigoBarras(agenciaVM As AgenciasViewModel) As String Implements IAgencia.calcularCodigoBarras
        Throw New NotImplementedException()
    End Function

    Public Function EnlaceSeguimiento(envio As EnviosAgencia) As String Implements IAgencia.EnlaceSeguimiento
        Throw New NotImplementedException()
    End Function


    Private Function rellenarPaises() As ObservableCollection(Of Pais)
        Return New ObservableCollection(Of Pais) From {
            New Pais(4, "AFGANISTAN", "AF"),
            New Pais(248, "ÅLAND", "AX"),
            New Pais(8, "ALBANIA", "AL"),
            New Pais(276, "ALEMANIA", "DE"),
            New Pais(20, "ANDORRA", "AD"),
            New Pais(24, "ANGOLA", "AO"),
            New Pais(660, "ANGUILA", "AI"),
            New Pais(10, "ANTARTIDA", "AQ"),
            New Pais(28, "ANTIGUA Y BARBUDA", "AG"),
            New Pais(530, "ANTILLAS HOLANDESAS", "AN"),
            New Pais(682, "ARABIA SAUDITA", "SA"),
            New Pais(12, "ARGELIA", "DZ"),
            New Pais(32, "ARGENTINA", "AR"),
            New Pais(51, "ARMENIA", "AM"),
            New Pais(533, "ARUBA", "AW"),
            New Pais(36, "AUSTRALIA", "AU"),
            New Pais(40, "AUSTRIA", "AT"),
            New Pais(31, "AZERBAIYAN", "AZ"),
            New Pais(44, "BAHAMAS", "BS"),
            New Pais(48, "BAHREIN", "BH"),
            New Pais(50, "BANGLADESH", "BD"),
            New Pais(52, "BARBADOS", "BB"),
            New Pais(112, "BIELORRUSIA", "BY"),
            New Pais(56, "BELGICA", "BE"),
            New Pais(84, "BELICE", "BZ"),
            New Pais(204, "BENIN", "BJ"),
            New Pais(60, "BERMUDAS", "BM"),
            New Pais(64, "BUTAN", "BT"),
            New Pais(68, "BOLIVIA", "BO"),
            New Pais(70, "BOSNIA Y HERZEGOVINA", "BA"),
            New Pais(72, "BOTSUANA", "BW"),
            New Pais(74, "ISLA BOUVET", "BV"),
            New Pais(76, "BRASIL", "BR"),
            New Pais(96, "BRUNEI", "BN"),
            New Pais(100, "BULGARIA", "BG"),
            New Pais(854, "BURKINA FASO", "BF"),
            New Pais(108, "BURUNDI", "BI"),
            New Pais(132, "CABO VERDE", "CV"),
            New Pais(136, "ISLAS CAIMAN", "KY"),
            New Pais(116, "CAMBOYA", "KH"),
            New Pais(120, "CAMERUN", "CM"),
            New Pais(124, "CANADA", "CA"),
            New Pais(140, "REPUBLICA CENTROAFRICANA", "CF"),
            New Pais(148, "CHAD", "TD"),
            New Pais(203, "REPUBLICA CHECA", "CZ"),
            New Pais(152, "CHILE", "CL"),
            New Pais(156, "CHINA", "CN"),
            New Pais(196, "CHIPRE", "CY"),
            New Pais(166, "ISLAS COCOS", "CC"),
            New Pais(170, "COLOMBIA", "CO"),
            New Pais(174, "COMORAS", "KM"),
            New Pais(178, "REPUBLICA DEL CONGO", "CG"),
            New Pais(180, "REPUBLICA DEMOCRATICA DEL CONGO", "CD"),
            New Pais(184, "ISLAS COOK", "CK"),
            New Pais(408, "COREA DEL NORTE", "KP"),
            New Pais(410, "COREA DEL SUR", "KR"),
            New Pais(384, "COSTA DE MARFIL", "CI"),
            New Pais(188, "COSTA RICA", "CR"),
            New Pais(191, "CROACIA", "HR"),
            New Pais(192, "CUBA", "CU"),
            New Pais(208, "DINAMARCA", "DK"),
            New Pais(212, "DOMINICA", "DM"),
            New Pais(214, "REPUBLICA DOMINICANA", "DO"),
            New Pais(218, "ECUADOR", "EC"),
            New Pais(818, "EGIPTO", "EG"),
            New Pais(222, "EL SALVADOR", "SV"),
            New Pais(784, "EMIRATOS ARABES UNIDOS", "AE"),
            New Pais(232, "ERITREA", "ER"),
            New Pais(703, "ESLOVAQUIA", "SK"),
            New Pais(705, "ESLOVENIA", "SI"),
            New Pais(724, "ESPAÑA", "ES"),
            New Pais(840, "ESTADOS UNIDOS", "US"),
            New Pais(581, "ISLAS ULTRAMARINAS DE ESTADOS UNIDOS", "UM"),
            New Pais(233, "ESTONIA", "EE"),
            New Pais(231, "ETIOPIA", "ET"),
            New Pais(234, "ISLAS FEROE", "FO"),
            New Pais(608, "FILIPINAS", "PH"),
            New Pais(246, "FINLANDIA", "FI"),
            New Pais(242, "FIYI", "FJ"),
            New Pais(250, "FRANCIA", "FR"),
            New Pais(266, "GABON", "GA"),
            New Pais(270, "GAMBIA", "GM"),
            New Pais(268, "GEORGIA", "GE"),
            New Pais(239, "ISLAS GEORGIAS DEL SUR Y SANDWICH DEL SUR", "GS"),
            New Pais(288, "GHANA", "GH"),
            New Pais(292, "GIBRALTAR", "GI"),
            New Pais(308, "GRANADA", "GD"),
            New Pais(300, "GRECIA", "GR"),
            New Pais(304, "GROENLANDIA", "GL"),
            New Pais(312, "GUADALUPE", "GP"),
            New Pais(316, "GUAM", "GU"),
            New Pais(320, "GUATEMALA", "GT"),
            New Pais(254, "GUAYANA FRANCESA", "GF"),
            New Pais(831, "GUERNSEY", "GG"),
            New Pais(324, "GUINEA", "GN"),
            New Pais(226, "GUINEA ECUATORIAL", "GQ"),
            New Pais(624, "GUINEA-BISSAU", "GW"),
            New Pais(328, "GUYANA", "GY"),
            New Pais(332, "HAITI", "HT"),
            New Pais(334, "ISLAS HEARD Y MCDONALD", "HM"),
            New Pais(340, "HONDURAS", "HN"),
            New Pais(344, "HONG KONG", "HK"),
            New Pais(348, "HUNGRIA", "HU"),
            New Pais(356, "INDIA", "IN"),
            New Pais(360, "INDONESIA", "ID"),
            New Pais(364, "IRAN", "IR"),
            New Pais(368, "IRAQ", "IQ"),
            New Pais(372, "IRLANDA", "IE"),
            New Pais(352, "ISLANDIA", "IS"),
            New Pais(376, "ISRAEL", "IL"),
            New Pais(380, "ITALIA", "IT"),
            New Pais(388, "JAMAICA", "JM"),
            New Pais(392, "JAPON", "JP"),
            New Pais(832, "JERSEY", "JE"),
            New Pais(400, "JORDANIA", "JO"),
            New Pais(398, "KAZAJISTAN", "KZ"),
            New Pais(404, "KENIA", "KE"),
            New Pais(417, "KIRGUISTAN", "KG"),
            New Pais(296, "KIRIBATI", "KI"),
            New Pais(414, "KUWAIT", "KW"),
            New Pais(418, "LAOS", "LA"),
            New Pais(426, "LESOTO", "LS"),
            New Pais(428, "LETONIA", "LV"),
            New Pais(422, "LIBANO", "LB"),
            New Pais(430, "LIBERIA", "LR"),
            New Pais(434, "LIBIA", "LY"),
            New Pais(438, "LIECHTENSTEIN", "LI"),
            New Pais(440, "LITUANIA", "LT"),
            New Pais(442, "LUXEMBURGO", "LU"),
            New Pais(446, "MACAO", "MO"),
            New Pais(807, "MACEDONIA", "MK"),
            New Pais(450, "MADAGASCAR", "MG"),
            New Pais(458, "MALASIA", "MY"),
            New Pais(454, "MALAWI", "MW"),
            New Pais(462, "MALDIVAS", "MV"),
            New Pais(466, "MALI", "ML"),
            New Pais(470, "MALTA", "MT"),
            New Pais(238, "ISLAS MALVINAS", "FK"),
            New Pais(833, "ISLA DE MAN", "IM"),
            New Pais(580, "ISLAS MARIANAS DEL NORTE", "MP"),
            New Pais(504, "MARRUECOS", "MA"),
            New Pais(584, "ISLAS MARSHALL", "MH"),
            New Pais(474, "MARTINICA", "MQ"),
            New Pais(480, "MAURICIO", "MU"),
            New Pais(478, "MAURITANIA", "MR"),
            New Pais(175, "MAYOTTE", "YT"),
            New Pais(484, "MEXICO", "MX"),
            New Pais(583, "MICRONESIA", "FM"),
            New Pais(498, "MOLDAVIA", "MD"),
            New Pais(492, "MONACO", "MC"),
            New Pais(496, "MONGOLIA", "MN"),
            New Pais(499, "MONTENEGRO", "ME"),
            New Pais(500, "MONTSERRAT", "MS"),
            New Pais(508, "MOZAMBIQUE", "MZ"),
            New Pais(104, "MYANMAR", "MM"),
            New Pais(516, "NAMIBIA", "NA"),
            New Pais(520, "NAURU", "NR"),
            New Pais(162, "ISLA DE NAVIDAD", "CX"),
            New Pais(524, "NEPAL", "NP"),
            New Pais(558, "NICARAGUA", "NI"),
            New Pais(562, "NIGER", "NE"),
            New Pais(566, "NIGERIA", "NG"),
            New Pais(570, "NIUE", "NU"),
            New Pais(574, "NORFOLK", "NF"),
            New Pais(578, "NORUEGA", "NO"),
            New Pais(540, "NUEVA CALEDONIA", "NC"),
            New Pais(554, "NUEVA ZELANDA", "NZ"),
            New Pais(512, "OMAN", "OM"),
            New Pais(528, "HOLANDA", "NL"),
            New Pais(586, "PAKISTAN", "PK"),
            New Pais(585, "PALAOS", "PW"),
            New Pais(275, "PALESTINA (ANP)", "PS"),
            New Pais(591, "PANAMA", "PA"),
            New Pais(598, "PAPUA NUEVA GUINEA", "PG"),
            New Pais(600, "PARAGUAY", "PY"),
            New Pais(604, "PERU", "PE"),
            New Pais(612, "ISLAS PITCAIRN", "PN"),
            New Pais(258, "POLINESIA FRANCESA", "PF"),
            New Pais(616, "POLONIA", "PL"),
            New Pais(620, "PORTUGAL", "PT"),
            New Pais(630, "PUERTO RICO", "PR"),
            New Pais(634, "QATAR", "QA"),
            New Pais(826, "REINO UNIDO", "GB"),
            New Pais(638, "REUNION", "RE"),
            New Pais(646, "RUANDA", "RW"),
            New Pais(642, "RUMANIA", "RO"),
            New Pais(643, "RUSIA", "RU"),
            New Pais(732, "SAHARA OCCIDENTAL", "EH"),
            New Pais(90, "ISLAS SALOMON", "SB"),
            New Pais(882, "SAMOA", "WS"),
            New Pais(16, "SAMOA AMERICANA", "AS"),
            New Pais(659, "SAINT KITTS AND NEVIS", "KN"),
            New Pais(674, "SAN MARINO", "SM"),
            New Pais(666, "SAN PEDRO Y MIQUELON", "PM"),
            New Pais(670, "SAN VICENTE Y LAS GRANADINAS", "VC"),
            New Pais(654, "SANTA HELENA", "SH"),
            New Pais(662, "SANTA LUCIA", "LC"),
            New Pais(678, "SANTO TOME Y PRINCIPE", "ST"),
            New Pais(686, "SENEGAL", "SN"),
            New Pais(688, "SERBIA", "RS"),
            New Pais(690, "SEYCHELLES", "SC"),
            New Pais(694, "SIERRA LEONA", "SL"),
            New Pais(702, "SINGAPUR", "SG"),
            New Pais(760, "SIRIA", "SY"),
            New Pais(706, "SOMALIA", "SO"),
            New Pais(144, "SRI LANKA", "LK"),
            New Pais(748, "SUAZILANDIA", "SZ"),
            New Pais(710, "SUDAFRICA", "ZA"),
            New Pais(736, "SUDAN", "SD"),
            New Pais(752, "SUECIA", "SE"),
            New Pais(756, "SUIZA", "CH"),
            New Pais(740, "SURINAM", "SR"),
            New Pais(744, "SVALBARD Y JAN MAYEN", "SJ"),
            New Pais(764, "TAILANDIA", "TH"),
            New Pais(158, "TAIWAN", "TW"),
            New Pais(834, "TANZANIA", "TZ"),
            New Pais(762, "TAYIKISTAN", "TJ"),
            New Pais(86, "TERRITORIO BRITANICO DEL OCEANO INDICO", "IO"),
            New Pais(260, "TERRITORIOS AUSTRALES FRANCESES", "TF"),
            New Pais(626, "TIMOR ORIENTAL", "TL"),
            New Pais(768, "TOGO", "TG"),
            New Pais(772, "TOKELAU", "TK"),
            New Pais(776, "TONGA", "TO"),
            New Pais(780, "TRINIDAD Y TOBAGO", "TT"),
            New Pais(788, "TUNEZ", "TN"),
            New Pais(796, "ISLAS TURCAS Y CAICOS", "TC"),
            New Pais(795, "TURKMENISTAN", "TM"),
            New Pais(792, "TURQUIA", "TR"),
            New Pais(798, "TUVALU", "TV"),
            New Pais(804, "UCRANIA", "UA"),
            New Pais(800, "UGANDA", "UG"),
            New Pais(858, "URUGUAY", "UY"),
            New Pais(860, "UZBEKISTAN", "UZ"),
            New Pais(548, "VANUATU", "VU"),
            New Pais(336, "CIUDAD DEL VATICANO", "VA"),
            New Pais(862, "VENEZUELA", "VE"),
            New Pais(704, "VIETNAM", "VN"),
            New Pais(92, "ISLAS VIRGENES BRITANICAS", "VG"),
            New Pais(850, "ISLAS VIRGENES ESTADOUNIDENSES", "VI"),
            New Pais(876, "WALLIS Y FUTUNA", "WF"),
            New Pais(887, "YEMEN", "YE"),
            New Pais(262, "YIBUTI", "DJ"),
            New Pais(894, "ZAMBIA", "ZM"),
            New Pais(716, "ZIMBABUE", "ZW")
        }
    End Function

End Class
