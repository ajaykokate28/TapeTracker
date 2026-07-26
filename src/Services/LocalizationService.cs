using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TapeTracker.Models;

namespace TapeTracker.Services;

/// <summary>
/// Runtime multi-language support: English / Marathi / Hindi / Gujarati.
/// Access via LocalizationService.Current.
/// Firing OnPropertyChanged(string.Empty) refreshes all bound labels at once.
/// </summary>
public partial class LocalizationService : ObservableObject
{
    public static LocalizationService Current { get; } = new();
    private LocalizationService() { }

    // 0 = English, 1 = Marathi, 2 = Hindi, 3 = Gujarati
    private int _langIndex;

    private const string PrefKey = "AppLangIndex";

    /// <summary>
    /// Restores the persisted language choice; on first launch (no
    /// preference stored yet) it inspects <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
    /// and picks the closest match so Marathi / Hindi / Gujarati speakers
    /// see the UI in their own script without hunting for the pill in
    /// the header. Call once from <c>App</c> startup.
    /// </summary>
    public void Initialize()
    {
        var stored = Preferences.Default.Get(PrefKey, -1);
        if (stored >= 0 && stored <= 3)
        {
            _langIndex = stored;
            OnPropertyChanged(string.Empty);
            return;
        }

        _langIndex = DetectFromOs();
        // Persist even the OS-derived value so we don't second-guess the
        // user later when their laptop's locale flips (traveling, VPN, etc.).
        Preferences.Default.Set(PrefKey, _langIndex);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Map the two-letter ISO code of the OS UI culture to our four-way
    /// language enum. Anything unrecognised falls back to English so a
    /// Spanish or Arabic desktop still shows a usable UI.
    /// </summary>
    private static int DetectFromOs()
    {
        try
        {
            var iso = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return iso switch
            {
                "mr" => 1,
                "hi" => 2,
                "gu" => 3,
                _    => 0
            };
        }
        catch
        {
            return 0;
        }
    }

    // Helper: pick string for current language
    private string T(string en, string mr, string hi, string gu) => _langIndex switch
    {
        1 => mr,
        2 => hi,
        3 => gu,
        _ => en
    };

    // ── Toggle ──────────────────────────────────────────────────────────────
    // Label shows the NEXT language to switch to
    public string LangToggleLabel => _langIndex switch
    {
        0 => "मराठी",
        1 => "हिंदी",
        2 => "ગુ",
        _ => "EN"
    };

    public string CurrentLanguageName => _langIndex switch
    {
        1 => "मराठी",
        2 => "हिंदी",
        3 => "ગુjrati",
        _ => "English"
    };

    [RelayCommand]
    private void ToggleLanguage()
    {
        _langIndex = (_langIndex + 1) % 4;
        Preferences.Default.Set(PrefKey, _langIndex);
        OnPropertyChanged(string.Empty);   // refresh every bound label
    }

    // ── General ─────────────────────────────────────────────────────────────
    public string AppSubtitle        => T("Tailor measurement manager",         "शिंपी मोजमाप व्यवस्थापक",       "दर्जी माप प्रबंधक",          "દરજી માપ વ્યવસ્થાપક");
    public string SearchPlaceholder  => T("Search name, phone or order…",       "नाव, फोन किंवा ऑर्डर शोधा…",   "नाम, फ़ोन या ऑर्डर खोजें…",  "નામ, ફોન અથવા ઓર્ડર શોધો…");
    public string RecentLabel        => T("Recent",                              "अलीकडील",                        "हाल का",                      "તાજેતર");
    public string OfLatestCustomers  => T("of latest customers",                 "अलीकडील ग्राहक",                 "हाल के ग्राहक",               "તાજેતરના ગ્રાહકો");
    public string ViewAllLabel       => T("📋 View All",                         "📋 सर्व पहा",                     "📋 सभी देखें",                "📋 બધા જુઓ");
    public string TotalLabel         => T("Total:",                              "एकूण:",                           "कुल:",                        "કુલ:");
    public string CustomersLabel     => T("customer(s)",                         "ग्राहक",                          "ग्राहक",                      "ગ્રાહકો");
    public string DeleteLabel        => T("Delete",                              "हटवा",                            "हटाएं",                       "કાઢી નાખો");
    public string EditLabel          => T("Edit",                                "संपादित करा",                    "संपादित करें",               "સંપાદિત કરો");
    public string ExportLabel        => T("Export",                              "निर्यात",                         "निर्यात",                     "નિકાસ");
    public string CancelLabel        => T("Cancel",                              "रद्द करा",                        "रद्द करें",                   "રદ કરો");
    public string SaveLabel          => T("Save Customer",                       "ग्राहक जतन करा",                  "ग्राहक सहेजें",               "ગ્રાહક સાચવો");

    // ── Empty state ──────────────────────────────────────────────────────────
    public string WelcomeTitle       => T("Welcome to TapeTracker!",               "TapeTracker मध्ये स्वागत!",         "TapeTracker में आपका स्वागत है!", "TapeTracker માં આપનું સ્વાગત!");
    public string WelcomeSubtitle    => T(
        "Save your customers' shirt and pant measurements in seconds. No more paper slips!",
        "ग्राहकांचे शर्ट व पँट मोजमाप सेकंदात जतन करा. आता कागदाची गरज नाही!",
        "ग्राहकों का शर्ट और पैंट माप सेकंड में सहेजें। अब कागज़ की पर्चियों की ज़रूरत नहीं!",
        "સેકન્ડમાં ગ્રાહકોનો શર્ટ અને પૅન્ટ માપ સાચવો. હવે કાગળ ચિઠ્ઠીઓની જરૂર નથી!");
    public string AddFirstCustomer   => T("+ Add First Customer",                "+ पहिला ग्राहक जोडा",             "+ पहला ग्राहक जोड़ें",         "+ પ્રથમ ગ્રાહક ઉમેરો");
    public string TipsTitle          => T("💡 Tips for getting started",          "💡 सुरुवातीसाठी टिप्स",           "💡 शुरुआत के लिए टिप्स",      "💡 શરૂ કરવા માટેની ટિપ્સ");
    public string TipsContent        => T(
        "• Tap + Add in the toolbar to add a customer\n• Swipe left on a card to delete\n• Tap the unit toggle to switch in / cm",
        "• + Add बटण दाबा ग्राहक जोडण्यासाठी\n• कार्ड उजव्या बाजूने स्वाइप करा हटवण्यासाठी\n• युनिट बटण दाबा in / cm बदलण्यासाठी",
        "• ग्राहक जोड़ने के लिए + Add दबाएं\n• कार्ड को बाईं ओर स्वाइप करें हटाने के लिए\n• in / cm बदलने के लिए यूनिट बटन दबाएं",
        "• ગ્રાહક ઉમેરવા + Add દબાવો\n• કાઢી નાખવા કાર્ડ ડાબે સ્વાઈપ કરો\n• in / cm બદલવા યુનિટ બટન દબાવો");
    public string NoCustomersFound   => T("No customers found",                  "कोणताही ग्राहक सापडला नाही",    "कोई ग्राहक नहीं मिला",       "કોઈ ગ્રાહક મળ્યા નહિ");
    public string TryDifferentSearch => T("Try a different search term.",        "वेगळा शोध वापरून पहा.",          "अलग खोज शब्द आज़माएं.",      "અલગ શોધ શબ્દ અજમાવો.");

    // ── Form Step 1 ──────────────────────────────────────────────────────────
    public string SetUnitHint         => T("Set unit then enter measurements",  "प्रथम युनिट निवडा",               "पहले यूनिट सेट करें",         "પ્રથમ એકમ પસંદ કરો");
    public string Step1Label          => T("Step 1",                             "चरण 1",                           "चरण 1",                       "પગલું 1");
    public string CustomerInfoLabel   => T("Customer Info",                      "ग्राहक माहिती",                   "ग्राहक जानकारी",              "ગ્રાહક માહિતી");
    public string FullNameLabel       => T("Full Name *",                        "पूर्ण नाव *",                     "पूरा नाम *",                  "પૂરું નામ *");
    public string FullNamePlaceholder => T("e.g. Rahul Sharma",                 "उदा. राहुल शर्मा",               "जैसे राहुल शर्मा",            "જેમ કે રાહુલ શર્મા");
    public string PhoneLabel          => T("Phone Number",                       "फोन नंबर",                        "फ़ोन नंबर",                   "ફોન નંબર");
    public string PhonePlaceholder    => T("e.g. 9876543210",                   "उदा. 9876543210",                  "जैसे 9876543210",             "જેમ કે 9876543210");
    public string OrderNoLabel        => T("Order / Bill No",                   "ऑर्डर / बिल नं.",                 "ऑर्डर / बिल नं.",             "ઓર્ડર / બિલ નં.");
    public string OrderNoPlaceholder  => T("e.g. ORD-001",                      "उदा. ORD-001",                    "जैसे ORD-001",                "જેમ કે ORD-001");
    public string OrderDateLabel      => T("Order Date",                         "ऑर्डर तारीख",                    "ऑर्डर तिथि",                  "ઓર્ડર તારીખ");
    public string DeliveryDateLabel   => T("🚚 Delivery Date",                   "🚚 डिलिव्हरी तारीख",             "🚚 डिलीवरी तिथि",             "🚚 ડિલિવરી તારીખ");
    public string DeliveryOptional    => T("(optional)",                         "(ऐच्छिक)",                        "(वैकल्पिक)",                  "(વૈકલ્પિક)");
    public string TickDeliveryHint    => T("Tick to set a delivery date",        "डिलिव्हरी तारीख सेट करण्यासाठी टिक करा", "डिलीवरी तिथि सेट करने के लिए टिक करें", "ડિલિવરી તારીખ સેટ કરવા ટિક કરો");
    public string RequiredFieldsNote  => T("Fields marked * are required.",     "* चिन्हांकित फील्ड आवश्यक आहेत.", "* चिह्नित फ़ील्ड आवश्यक हैं।", "* ચિહ્નિત ફીલ્ડ આવશ્યક છે.");

    // ── Form Step 2 ──────────────────────────────────────────────────────────
    public string Step2Label  => T("Step 2",  "चरण 2",  "चरण 2",  "પગલું 2");
    public string ShirtLabel  => T("Shirt",   "शर्ट",   "शर्ट",   "શર્ટ");
    public string ShirtHint   => T(
        "Enter body measurements taken with a measuring tape. Leave fields blank if not applicable.",
        "मोजपट्टीने घेतलेले मोजमाप प्रविष्ट करा. लागू नसल्यास रिकामे सोडा.",
        "मापने वाले टेप से लिए गए माप दर्ज करें। यदि लागू न हो तो खाली छोड़ें।",
        "માપ પટ્ટીથી લીધેલ માપ દાખલ કરો. જો લાગુ ન હોય તો ખાલી છોડો.");
    public string ShirtTip    => T(
        "Tip: Measure around the fullest part for chest, waist and hip.",
        "टीप: छाती, कंबर व नितंबासाठी सर्वात रुंद भागाचे मोजमाप घ्या.",
        "टिप: छाती, कमर और कूल्हे के लिए सबसे चौड़े हिस्से का माप लें।",
        "ટિપ: છાતી, કમર અને નિતંબ માટે સૌથી પહોળા ભાગ ફરતે માપો.");

    // ── Form Step 3 ──────────────────────────────────────────────────────────
    public string Step3Label  => T("Step 3",  "चरण 3",  "चरण 3",  "પગલું 3");
    public string PantLabel   => T("Pant",    "पँट",    "पैंट",   "પૅન્ટ");
    public string PantHint    => T(
        "Measure the inside leg for length and around the widest part for seat and hip.",
        "लांबीसाठी आतील पायाचे मोजमाप घ्या. सीट व नितंबासाठी रुंद भागाचे मोजमाप घ्या.",
        "लंबाई के लिए अंदरूनी पैर का माप लें और सीट व कूल्हे के लिए चौड़े हिस्से का।",
        "લંબાઈ માટે અંદરના પગ ફરતે માપો, સીટ અને નિતંબ માટે પહોળા ભાગ ફરતે.");

    // ── Measurement field labels ─────────────────────────────────────────────
    public string LengthLabel   => T("Length",   "लांबी",   "लंबाई",  "લંબાઈ");
    public string ChestLabel    => T("Chest",    "छाती",    "छाती",   "છાતી");
    public string WaistLabel    => T("Waist",    "कंबर",    "कमर",    "કમર");
    public string HipLabel      => T("Hip",      "नितंब",   "कूल्हे", "નિતંબ");
    public string ShoulderLabel => T("Shoulder", "खांदा",   "कंधा",   "ખભો");
    public string SleeveLabel   => T("Sleeve",   "बाही",    "बाजू",   "બાંય");
    public string CuffLabel     => T("Cuff",     "कफ",      "कफ़",    "કફ");
    public string CollarLabel   => T("Collar",   "कॉलर",    "कॉलर",   "કોલર");
    public string ThighLabel    => T("Thigh",    "मांडी",   "जांघ",   "જાંઘ");
    public string AnkleLabel    => T("Ankle",    "घोटा",    "टखना",   "ઘૂંટી");
    public string KneeLabel     => T("Knee",     "गुडघा",   "घुटना",  "ઘૂંટણ");
    public string SeatLabel     => T("Seat",     "सीट",     "सीट",    "સીટ");

    // ── Detail page ──────────────────────────────────────────────────────────
    public string ShirtSectionLabel => T("👔  SHIRT · ", "👔  शर्ट · ", "👔  शर्ट · ", "👔  શર્ટ · ");
    public string PantSectionLabel  => T("👖  PANT · ",  "👖  पँट · ",  "👖  पैंट · ", "👖  પૅન્ટ · ");
    public string DeliveryPrefix    => T("Delivery: ",   "डिलिव्हरी: ", "डिलीवरी: ",  "ડિલિવરી: ");

    // ── Home / list actions ─────────────────────────────────────────────────
    public string ScanSlipLabel          => T("Scan Slip",             "स्लिप स्कॅन करा",     "स्लिप स्कैन करें",    "સ્લિપ સ્કેન કરો");
    public string AddCustomerLabel       => T("Add Customer",          "ग्राहक जोडा",         "ग्राहक जोड़ें",       "ગ્રાહક ઉમેરો");
    public string FilterByTagLabel       => T("Filter by tag",         "टॅगनुसार फिल्टर",    "टैग द्वारा फ़िल्टर",   "ટૅગ પ્રમાણે ફિલ્ટર");
    public string FilterByTagPlaceholder => T("Filter by family / group tag…", "कुटुंब / गट टॅगने फिल्टर करा…", "परिवार / समूह टैग से फ़िल्टर करें…", "કુટુંબ / જૂથ ટૅગથી ફિલ્ટર કરો…");
    public string AllLabel               => T("All",                   "सर्व",                 "सभी",                 "બધું");
    public string BackupLabel            => T("Backup",                "बॅकअप",               "बैकअप",               "બૅકઅપ");
    public string RestoreLabel           => T("Restore",               "पुनर्संचयन",           "पुनर्स्थापित",         "પુનઃસ્થાપિત");

    // ── All Customers page ──────────────────────────────────────────────────
    public string DirectoryLabel     => T("DIRECTORY",         "निर्देशिका",         "निर्देशिका",          "ડિરેક્ટરી");
    public string AllCustomersLabel  => T("All Customers",     "सर्व ग्राहक",         "सभी ग्राहक",           "બધા ગ્રાહકો");
    public string ExportExcelLabel   => T("Export Excel",      "एक्सेल निर्यात",      "एक्सेल निर्यात",       "એક્સેલ નિકાસ");
    public string FilterByStatusLabel => T("FILTER BY STATUS", "स्थितीनुसार फिल्टर",  "स्थिति द्वारा फ़िल्टर", "સ્થિતિ પ્રમાણે ફિલ્ટર");
    public string StatusReceived     => T("Received",          "प्राप्त",              "प्राप्त",              "પ્રાપ્ત");
    public string StatusCutting      => T("Cutting",           "कटिंग",                "कटाई",                 "કટિંગ");
    public string StatusStitching    => T("Stitching",         "शिवणकाम",              "सिलाई",                "સિલાઈ");
    public string StatusReady        => T("Ready",             "तयार",                 "तैयार",                "તૈયાર");
    public string StatusDelivered    => T("Delivered",         "वितरित",               "वितरित",               "વિતરિત");

    /// <summary>
    /// Maps an <see cref="OrderStatus"/> to its localized display string, so every
    /// call site (order-card pills, dashboard rows, PDFs, share text) uses the
    /// same translation as the status filter chips.
    /// </summary>
    public string LocalizeStatus(OrderStatus status) => status switch
    {
        OrderStatus.Received  => StatusReceived,
        OrderStatus.Cutting   => StatusCutting,
        OrderStatus.Stitching => StatusStitching,
        OrderStatus.Ready     => StatusReady,
        OrderStatus.Delivered => StatusDelivered,
        _                     => status.ToString()
    };

    // ── Measurement form (new UX strings) ───────────────────────────────────
    public string FamilyGroupTagLabel   => T("FAMILY / GROUP TAG", "कुटुंब / गट टॅग",       "परिवार / समूह टैग",    "કુટુંબ / જૂથ ટૅગ");
    public string FamilyGroupTagPh      => T("e.g. Sharma Family",  "उदा. शर्मा कुटुंब",      "जैसे शर्मा परिवार",    "જેમ કે શર્મા કુટુંબ");
    public string OptionalLabel         => T("(optional)",          "(ऐच्छिक)",              "(वैकल्पिक)",           "(વૈકલ્પિક)");
    public string DidYouMeanExistingLabel => T("Did you mean an existing customer?", "आपण अस्तित्वात असलेल्या ग्राहकाला म्हणत आहात का?", "क्या आप किसी मौजूदा ग्राहक की बात कर रहे हैं?", "શું તમે કોઈ હાલના ગ્રાહકનો ઉલ્લેખ કરી રહ્યા છો?");
    public string CopyFromLastOrderLabel => T("Copy from last order", "मागील ऑर्डरवरून कॉपी करा", "पिछले ऑर्डर से कॉपी करें", "છેલ્લા ઓર્ડરથી કૉપી કરો");
    public string ShowAdvancedFieldsLabel => T("+ Show advanced fields", "+ अधिक फील्ड दाखवा",   "+ और फ़ील्ड दिखाएं",   "+ વધુ ફીલ્ડ બતાવો");
    public string HideAdvancedFieldsLabel => T("− Hide advanced fields", "− अधिक फील्ड लपवा",    "− अतिरिक्त फ़ील्ड छिपाएं", "− વધુ ફીલ્ડ છુપાવો");
    public string Step4Label            => T("Step 4",           "चरण 4",                 "चरण 4",                "પગલું 4");
    public string NotesLabel            => T("Notes",            "टिपा",                  "नोट्स",                "નોંધ");
    public string NotesHint             => T("Fabric, fitting preferences, delivery hints…", "कापड, फिटिंग आवडी, डिलिव्हरी सूचना…", "कपड़ा, फिटिंग वरीयता, डिलीवरी संकेत…", "કપડું, ફિટિંગ પસંદગી, ડિલિવરી સૂચનો…");
    public string NotesPlaceholder      => T("Optional notes (leave blank if none)", "ऐच्छिक टिपा (नसल्यास रिकामे सोडा)", "वैकल्पिक नोट्स (न हो तो खाली छोड़ें)", "વૈકલ્પિક નોંધ (ન હોય તો ખાલી છોડો)");

    // ── Customer Detail page (order actions) ────────────────────────────────
    public string AdvanceLabel      => T("Advance",       "पुढे नेणे",            "आगे बढ़ाएं",          "આગળ વધારો");
    public string RepeatLabel       => T("Repeat",        "पुनरावृत्ती",          "दोहराएं",              "પુનરાવર્તન");
    public string PdfLabel          => T("PDF",           "PDF",                  "PDF",                  "PDF");
    public string QrLabel           => T("QR",            "QR",                   "QR",                   "QR");
    public string JsonLabel         => T("JSON",          "JSON",                 "JSON",                 "JSON");
    public string NotifyLabel       => T("Notify",        "सूचना द्या",           "सूचित करें",          "સૂચિત કરો");
    public string NoOrdersYetLabel  => T("No orders yet", "अद्याप ऑर्डर नाहीत",  "अभी तक कोई ऑर्डर नहीं", "હજુ કોઈ ઓર્ડર નથી");
    public string DuePrefix         => T("Due ",          "देय ",                 "देय ",                 "બાકી ");

    // ── Undo / misc list-page snackbar ──────────────────────────────────────
    public string UndoLabel          => T("UNDO",                       "पूर्ववत",                    "पूर्ववत करें",              "પાછું લાવો");

    // ── Splash page ─────────────────────────────────────────────────────────
    public string PrecisionTailoringLabel => T("PRECISION TAILORING",   "अचूक शिवणकाम",              "सटीक सिलाई",                "ચોકસાઈભરી સિલાઈ");

    // ── Measurement form page header ────────────────────────────────────────
    public string MeasurementHeader  => T("MEASUREMENT",                "मोजमाप",                    "माप",                       "માપ");

    // ── Dashboard ───────────────────────────────────────────────────────────
    public string ExportShortLabel        => T("Export",                "निर्यात",                    "निर्यात",                    "નિકાસ");
    public string RefreshLabel            => T("Refresh",               "रीफ्रेश",                    "रीफ़्रेश",                   "તાજું કરો");
    public string PinLabel                => T("PIN",                   "PIN",                        "PIN",                        "PIN");
    public string BusinessOverviewLabel   => T("Business Overview",     "व्यवसाय अवलोकन",             "व्यापार अवलोकन",             "વ્યવસાય અવલોકન");
    public string CustomersCountLabel     => T("Customers",             "ग्राहक",                     "ग्राहक",                     "ગ્રાહકો");
    public string TotalOrdersLabel        => T("Total Orders",          "एकूण ऑर्डर",                 "कुल ऑर्डर",                   "કુલ ઓર્ડર");
    public string DueThisWeekLabel        => T("Due This Week",         "या आठवड्यात देय",            "इस सप्ताह देय",              "આ અઠવાડિયે બાકી");
    public string OverdueLabel            => T("Overdue",               "मुदत उलटलेली",               "अतिदेय",                     "મુદત વીતી");
    public string OrderPipelineLabel      => T("ORDER PIPELINE",        "ऑर्डर पाइपलाइन",             "ऑर्डर पाइपलाइन",            "ઓર્ડર પાઇપલાઇન");
    public string StatusBreakdownLabel    => T("Status Breakdown",      "स्थिती वर्गीकरण",            "स्थिति विवरण",               "સ્થિતિ વર્ગીકરણ");
    public string TrendLabel              => T("TREND",                 "प्रवृत्ती",                   "प्रवृत्ति",                  "વલણ");
    public string ActivityLabel           => T("ACTIVITY",              "क्रियाकलाप",                 "गतिविधि",                    "પ્રવૃત્તિ");
    public string RecentOrdersLabel       => T("Recent Orders",         "अलीकडील ऑर्डर",              "हाल के ऑर्डर",                "તાજેતરના ઓર્ડર");

    // ── QR page ─────────────────────────────────────────────────────────────
    public string ShareLabel              => T("Share",                 "शेअर करा",                   "साझा करें",                  "શેર કરો");
    public string OrderQrCodeLabel        => T("Order QR Code",         "ऑर्डर QR कोड",              "ऑर्डर QR कोड",              "ઓર્ડર QR કોડ");
    public string ScanToOpenLabel         => T("Scan to open this order on any device",
                                              "कोणत्याही डिव्हाइसवर हा ऑर्डर उघडण्यासाठी स्कॅन करा",
                                              "किसी भी डिवाइस पर इस ऑर्डर को खोलने के लिए स्कैन करें",
                                              "કોઈપણ ડિવાઇસ પર આ ઓર્ડર ખોલવા સ્કેન કરો");
    public string OrderDetailsLabel       => T("ORDER DETAILS",         "ऑर्डर तपशील",                "ऑर्डर विवरण",                "ઓર્ડર વિગતો");
    public string CustomerFieldLabel      => T("Customer",              "ग्राहक",                     "ग्राहक",                     "ગ્રાહક");
    public string StatusFieldLabel        => T("Status",                "स्थिती",                     "स्थिति",                     "સ્થિતિ");
    public string DateFieldLabel          => T("Date",                  "तारीख",                      "तिथि",                       "તારીખ");
    public string OrderNoFieldLabel       => T("Order #",                "ऑर्डर #",                    "ऑर्डर #",                    "ઓર્ડર #");
    public string ShareQrCodeLabel        => T("Share QR Code",         "QR कोड शेअर करा",            "QR कोड साझा करें",           "QR કોડ શેર કરો");

    // ── OCR Scan page ───────────────────────────────────────────────────────
    public string ScanMeasurementSlipLabel => T("Scan Measurement Slip", "मोजमाप स्लिप स्कॅन करा",   "माप स्लिप स्कैन करें",       "માપ સ્લિપ સ્કેન કરો");
    public string CaptureLabel            => T("CAPTURE",               "कॅप्चर",                     "कैप्चर",                     "કૅપ્ચર");
    public string TakePhotoOfSlipLabel    => T("Take a photo of the measurement slip",
                                              "मोजमाप स्लिपचा फोटो घ्या",
                                              "माप स्लिप का फ़ोटो लें",
                                              "માપ સ્લિપનો ફોટો લો");
    public string ReviewLabel             => T("REVIEW",                "पुनरावलोकन",                 "समीक्षा",                    "સમીક્ષા");
    public string ViewRawOcrLabel         => T("View raw OCR text",     "मूळ OCR मजकूर पहा",         "मूल OCR टेक्स्ट देखें",       "મૂળ OCR ટેક્સ્ટ જુઓ");
    public string SaveShortLabel          => T("Save",                  "जतन करा",                    "सहेजें",                     "સાચવો");
    public string CaptureHeaderHintLabel  => T("Capture a photo or pick from gallery to auto-fill",
                                              "फोटो घ्या किंवा गॅलरीतून निवडा (आपोआप भरण्यासाठी)",
                                              "फ़ोटो लें या गैलरी से चुनें (स्वतः भरने के लिए)",
                                              "ફોટો લો અથવા ગેલેરીથી પસંદ કરો (આપોઆપ ભરવા માટે)");
    public string CameraLabel             => T("Camera",                "कॅमेरा",                     "कैमरा",                      "કૅમેરા");
    public string GalleryLabel            => T("Gallery",               "गॅलरी",                      "गैलरी",                      "ગેલેરી");
    public string ShirtMeasurementsLabel  => T("Shirt Measurements",    "शर्ट मोजमाप",                "शर्ट माप",                   "શર્ટ માપ");
    public string PantMeasurementsLabel   => T("Pant Measurements",     "पँट मोजमाप",                 "पैंट माप",                    "પૅન્ટ માપ");
    public string NameStarLabel           => T("NAME *",                "नाव *",                      "नाम *",                      "નામ *");
    public string PhoneCap                => T("PHONE",                 "फोन",                        "फ़ोन",                       "ફોન");
    public string OrderNoCap              => T("ORDER NO",              "ऑर्डर क्र.",                 "ऑर्डर नं.",                   "ઓર્ડર નં.");
    public string DateCap                 => T("DATE",                  "तारीख",                      "तिथि",                       "તારીખ");
    public string ProcessingImageMsg      => T("Processing image…",     "चित्र प्रक्रिया करत आहे…",  "छवि संसाधित हो रही है…",     "છબી પ્રક્રિયા થઈ રહી છે…");
    public string CancelledMsg            => T("Cancelled.",             "रद्द केले.",                 "रद्द किया गया।",             "રદ કરાયું.");
    public string NoMeasurementsMsg       => T("⚠️ No measurements detected. Please fill in manually.",
                                              "⚠️ मोजमाप सापडली नाहीत. कृपया मॅन्युअली भरा.",
                                              "⚠️ कोई माप नहीं मिला। कृपया मैन्युअल रूप से भरें।",
                                              "⚠️ કોઈ માપ મળ્યા નથી. કૃપા કરીને જાતે ભરો.");
    public string FieldsExtractedFmt      => T("✅ {0} field(s) extracted. Review and save.",
                                              "✅ {0} फील्ड मिळाले. पुनरावलोकन करा आणि जतन करा.",
                                              "✅ {0} फ़ील्ड निकाले गए। समीक्षा करके सहेजें।",
                                              "✅ {0} ફીલ્ડ મળ્યા. સમીક્ષા કરીને સાચવો.");
    public string ErrorPrefix             => T("Error: ",                "त्रुटी: ",                    "त्रुटि: ",                    "ભૂલ: ");

    // ── Customer Detail extras ──────────────────────────────────────────────
    public string AddOrderLabel           => T("+ Order",                "+ ऑर्डर",                    "+ ऑर्डर",                    "+ ઓર્ડર");
    public string UnitPrefix              => T("Unit: ",                 "युनिट: ",                    "यूनिट: ",                    "એકમ: ");
    public string FamilyPrefix            => T("Family: ",               "कुटुंब: ",                   "परिवार: ",                   "કુટુંબ: ");
    public string TapAddOrderHint         => T("Tap the '+ Order' button to create the first order for this customer.",
                                              "या ग्राहकासाठी पहिला ऑर्डर तयार करण्यासाठी '+ ऑर्डर' बटण दाबा.",
                                              "इस ग्राहक के लिए पहला ऑर्डर बनाने हेतु '+ ऑर्डर' बटन दबाएं.",
                                              "આ ગ્રાહક માટે પ્રથમ ઓર્ડર બનાવવા '+ ઓર્ડર' બટન દબાવો.");

    // ── PIN page ────────────────────────────────────────────────────────────
    public string ConfirmPinLabel         => T("CONFIRM PIN",           "PIN निश्चित करा",            "PIN की पुष्टि करें",         "PIN પુષ્ટિ કરો");
    public string RemovePinLabel          => T("Remove PIN",            "PIN काढा",                   "PIN हटाएं",                  "PIN દૂર કરો");
    public string EnterPinTitle           => T("Enter PIN",              "PIN प्रविष्ट करा",           "PIN दर्ज करें",              "PIN દાખલ કરો");
    public string SetPinTitle             => T("Set a PIN",              "PIN सेट करा",                "PIN सेट करें",              "PIN સેટ કરો");
    public string ChangeOrRemovePinTitle  => T("Change / Remove PIN",    "PIN बदला / काढा",           "PIN बदलें / हटाएं",          "PIN બદલો / દૂર કરો");
    public string EnterPinAgainMsg        => T("Enter PIN again to confirm",
                                              "पुष्टीसाठी पुन्हा PIN प्रविष्ट करा",
                                              "पुष्टि के लिए PIN दोबारा दर्ज करें",
                                              "પુષ્ટિ માટે PIN ફરીથી દાખલ કરો");
    public string IncorrectPinMsg         => T("Incorrect PIN. Try again.",
                                              "चुकीचा PIN. पुन्हा प्रयत्न करा.",
                                              "गलत PIN. फिर से कोशिश करें.",
                                              "ખોટો PIN. ફરી પ્રયાસ કરો.");
    public string PinsDontMatchMsg        => T("PINs do not match. Try again.",
                                              "PIN जुळत नाहीत. पुन्हा प्रयत्न करा.",
                                              "PIN मेल नहीं खाते. फिर से कोशिश करें.",
                                              "PINs મેળ ખાતા નથી. ફરી પ્રયાસ કરો.");
    public string PinSetSuccessMsg        => T("PIN set successfully.",
                                              "PIN यशस्वीरित्या सेट झाला.",
                                              "PIN सफलतापूर्वक सेट हो गया।",
                                              "PIN સફળતાપૂર્વક સેટ થયો.");
    public string PinRemovedMsg           => T("PIN removed.",           "PIN काढला.",                 "PIN हटा दिया गया।",          "PIN દૂર કરાયો.");

    // ── Auth: Organization registration ─────────────────────────────────────
    public string OrgRegisterTitle        => T("Set up your shop",       "तुमचे दुकान सेट करा",         "अपनी दुकान सेट करें",         "તમારી દુકાન સેટ કરો");
    public string OrgRegisterSubtitle     => T("One-time setup. Create your shop and the first admin account.",
                                              "एकदाच सेटअप. तुमचे दुकान आणि पहिले प्रशासक खाते तयार करा.",
                                              "एक-बार सेटअप। अपनी दुकान और पहला व्यवस्थापक खाता बनाएं।",
                                              "એક-વારના સેટઅપ. તમારી દુકાન અને પ્રથમ એડમિન એકાઉન્ટ બનાવો.");
    public string ShopSectionLabel        => T("SHOP",                   "दुकान",                       "दुकान",                       "દુકાન");
    public string AdminSectionLabel       => T("ADMIN ACCOUNT",          "प्रशासक खाते",                "व्यवस्थापक खाता",             "એડમિન એકાઉન્ટ");
    public string ShopNameLabel           => T("Shop Name *",            "दुकानाचे नाव *",              "दुकान का नाम *",              "દુકાનનું નામ *");
    public string ShopNamePh              => T("e.g. Sharma Tailors",    "उदा. शर्मा टेलर्स",           "जैसे शर्मा टेलर्स",           "જેમ કે શર્મા ટેલર્સ");
    public string ShopPhoneLabel          => T("Shop Phone",             "दुकान फोन",                   "दुकान फ़ोन",                  "દુકાન ફોન");
    public string ShopAddressLabel        => T("Address",                "पत्ता",                       "पता",                         "સરનામું");
    public string UsernameLabel           => T("Username *",             "वापरकर्ता नाव *",             "उपयोगकर्ता नाम *",            "વપરાશકર્તા નામ *");
    public string UsernamePh              => T("e.g. admin",             "उदा. admin",                  "जैसे admin",                  "જેમ કે admin");
    public string DisplayNameLabel        => T("Your Name",              "तुमचे नाव",                   "आपका नाम",                    "તમારું નામ");
    public string DisplayNamePh           => T("e.g. Rahul Sharma",      "उदा. राहुल शर्मा",            "जैसे राहुल शर्मा",            "જેમ કે રાહુલ શર્મા");
    public string PasswordLabel           => T("Password *",             "पासवर्ड *",                   "पासवर्ड *",                   "પાસવર્ડ *");
    public string PasswordPh              => T("at least 4 characters",  "किमान 4 अक्षरे",              "कम से कम 4 वर्ण",             "ઓછામાં ઓછા 4 અક્ષર");
    public string ConfirmPasswordLabel    => T("Confirm Password *",     "पासवर्डची पुष्टी करा *",      "पासवर्ड पुष्टि करें *",       "પાસવર્ડની પુષ્ટિ કરો *");
    public string CreateShopButton        => T("Create Shop & Sign In",  "दुकान तयार करा आणि साइन इन", "दुकान बनाएं और साइन इन करें",  "દુકાન બનાવો અને સાઇન ઇન");
    public string PasswordMismatch        => T("Passwords do not match.",
                                              "पासवर्ड जुळत नाहीत.",
                                              "पासवर्ड मेल नहीं खाते।",
                                              "પાસવર્ડ મેળ ખાતા નથી.");

    // ── Auth: Login ─────────────────────────────────────────────────────────
    public string LoginTitle              => T("Welcome back",           "पुन्हा स्वागत आहे",            "फिर से स्वागत है",            "ફરી સ્વાગત");
    public string LoginSubtitle           => T("Sign in to continue",    "पुढे जाण्यासाठी साइन इन करा", "जारी रखने के लिए साइन इन करें", "ચાલુ રાખવા સાઇન ઇન કરો");
    public string SignInLabel             => T("Sign In",                "साइन इन",                    "साइन इन",                     "સાઇન ઇન");
    public string RememberMeLabel         => T("Remember me",            "मला लक्षात ठेवा",             "मुझे याद रखें",               "મને યાદ રાખો");
    public string InvalidCredentialsMsg   => T("Invalid username or password.",
                                              "चुकीचे वापरकर्ता नाव किंवा पासवर्ड.",
                                              "अमान्य उपयोगकर्ता नाम या पासवर्ड।",
                                              "અમાન્ય વપરાશકર્તા નામ અથવા પાસવર્ડ.");
    public string LogoutLabel             => T("Logout",                 "साइन आउट",                   "लॉग आउट",                    "લૉગ આઉટ");
    public string SignedInAsFmt           => T("Signed in as {0}",       "{0} म्हणून साइन इन",         "{0} के रूप में साइन इन",      "{0} તરીકે સાઇન ઇન");

    // ── Auth: Roles + confirmations ─────────────────────────────────────────
    public string AdminRoleLabel          => T("Admin",                  "प्रशासक",                     "व्यवस्थापक",                 "એડમિન");
    public string EmployeeRoleLabel       => T("Employee",               "कर्मचारी",                    "कर्मचारी",                    "કર્મચારી");
    public string ConfirmLogoutTitle      => T("Sign out?",              "साइन आउट करायचे?",           "साइन आउट करें?",              "સાઇન આઉટ કરવું?");
    public string ConfirmLogoutMsg        => T("You'll need to enter your password again to continue.",
                                              "पुढे जाण्यासाठी तुम्हाला पुन्हा पासवर्ड टाकावा लागेल.",
                                              "जारी रखने के लिए आपको फिर से पासवर्ड डालना होगा।",
                                              "ચાલુ રાખવા તમારે ફરીથી પાસવર્ડ દાખલ કરવો પડશે.");
    public string YesSignOutLabel         => T("Yes, sign out",          "होय, साइन आउट",              "हाँ, साइन आउट",              "હા, સાઇન આઉટ");
    public string PermissionDeniedTitle   => T("Not allowed",            "अनुमती नाही",                 "अनुमति नहीं",                 "મંજૂરી નથી");
    public string PermissionDeniedMsg     => T("Only admins can do that. Ask your admin to make the change.",
                                              "फक्त प्रशासकच हे करू शकतात. बदल करण्यासाठी आपल्या प्रशासकाला विचारा.",
                                              "केवल व्यवस्थापक ही यह कर सकते हैं। बदलाव के लिए अपने व्यवस्थापक से पूछें।",
                                              "માત્ર એડમિન જ આ કરી શકે. બદલવા માટે તમારા એડમિનને પૂછો.");
    public string OkLabel                 => T("OK",                     "ठीक आहे",                     "ठीक है",                      "બરાબર");
    public string ContinueLabel           => T("Continue",               "पुढे चला",                     "जारी रखें",                   "ચાલુ રાખો");
    public string BackLabel               => T("Back",                   "मागे",                        "वापस",                        "પાછળ");
    public string ConfigureLabel          => T("Configure",              "कॉन्फिगर करा",                "कॉन्फ़िगर करें",              "કૉન્ફિગર કરો");
    public string BackToLoginLabel        => T("Back to sign-in",        "साइन इन वर परत",             "साइन इन पर वापस",             "સાઇન ઇન પર પાછા");

    // ── Password recovery (login page + settings) ──────────────────────────
    public string ForgotPasswordLinkLabel => T("Forgot password?",       "पासवर्ड विसरलात?",           "पासवर्ड भूल गए?",              "પાસવર્ડ ભૂલી ગયા?");
    public string ForgotPasswordTitle     => T("Reset your password",    "पासवर्ड रीसेट करा",           "पासवर्ड रीसेट करें",           "તમારો પાસવર્ડ રીસેટ કરો");
    public string ForgotPasswordSubtitle  => T("Answer your recovery question to set a new password.",
                                              "नवीन पासवर्ड सेट करण्यासाठी आपला रिकव्हरी प्रश्न सोडवा.",
                                              "नया पासवर्ड सेट करने के लिए अपने रिकवरी प्रश्न का उत्तर दें।",
                                              "નવો પાસવર્ડ સેટ કરવા તમારો રિકવરી પ્રશ્ન જવાબ આપો.");
    public string ForgotStep1Hint         => T("Enter the username you sign in with. We'll look up your recovery question next.",
                                              "आपण साइन इन करता तो वापरकर्ता नाव टाका. आम्ही तुमचा रिकव्हरी प्रश्न पाहू.",
                                              "जिस उपयोगकर्ता नाम से आप साइन इन करते हैं वह डालें। हम आपका रिकवरी प्रश्न देखेंगे।",
                                              "તમે જે વપરાશકર્તા નામથી સાઇન ઇન કરો છો તે દાખલ કરો. અમે તમારો રિકવરી પ્રશ્ન જોઈશું.");
    public string ForgotEnterUsernameMsg  => T("Please enter your username.",
                                              "कृपया आपले वापरकर्ता नाव टाका.",
                                              "कृपया अपना उपयोगकर्ता नाम डालें।",
                                              "કૃપા કરીને તમારું વપરાશકર્તા નામ દાખલ કરો.");
    public string ForgotNoQuestionMsg     => T("This account has no recovery question set up. Ask an admin in your shop to reset your password.",
                                              "या खात्यासाठी रिकव्हरी प्रश्न सेट केलेला नाही. तुमच्या दुकानातील प्रशासकाला पासवर्ड रीसेट करण्यास सांगा.",
                                              "इस खाते के लिए रिकवरी प्रश्न सेट नहीं है। अपनी दुकान के व्यवस्थापक से पासवर्ड रीसेट करने को कहें।",
                                              "આ ખાતા માટે રિકવરી પ્રશ્ન સેટ કરેલો નથી. તમારી દુકાનના એડમિનને પાસવર્ડ રીસેટ કરવા કહો.");
    public string ForgotYourQuestionLabel => T("YOUR RECOVERY QUESTION",  "तुमचा रिकव्हरी प्रश्न",       "आपका रिकवरी प्रश्न",          "તમારો રિકવરી પ્રશ્ન");
    public string ForgotAnswerLabel       => T("Your answer *",           "तुमचे उत्तर *",                "आपका उत्तर *",                "તમારો જવાબ *");
    public string ForgotAnswerPh          => T("Type the answer",         "उत्तर टाइप करा",              "उत्तर टाइप करें",             "જવાબ ટાઇપ કરો");
    public string NewPasswordPh           => T("At least 4 characters",   "किमान 4 अक्षरे",              "कम से कम 4 अक्षर",            "ઓછામાં ઓછા 4 અક્ષરો");
    public string ConfirmPasswordPh       => T("Retype new password",     "नवीन पासवर्ड पुन्हा टाका",     "नया पासवर्ड फिर से डालें",     "નવો પાસવર્ડ ફરી લખો");
    public string ForgotEnterAnswerMsg    => T("Please answer your recovery question.",
                                              "कृपया तुमच्या रिकव्हरी प्रश्नाचे उत्तर द्या.",
                                              "कृपया अपने रिकवरी प्रश्न का उत्तर दें।",
                                              "કૃપા કરીને તમારા રિકવરી પ્રશ્નનો જવાબ આપો.");
    public string ForgotPasswordTooShortMsg => T("New password must be at least 4 characters.",
                                                "नवीन पासवर्ड किमान 4 अक्षरांचा असावा.",
                                                "नया पासवर्ड कम से कम 4 अक्षरों का होना चाहिए।",
                                                "નવો પાસવર્ડ ઓછામાં ઓછો 4 અક્ષરોનો હોવો જોઈએ.");
    public string ForgotPasswordMismatchMsg => T("Passwords don't match. Please retype.",
                                                "पासवर्ड जुळत नाहीत. कृपया पुन्हा टाका.",
                                                "पासवर्ड मेल नहीं खा रहे। कृपया फिर से डालें।",
                                                "પાસવર્ડ મેળ ખાતા નથી. કૃપા કરીને ફરી લખો.");
    public string ForgotWrongAnswerMsg    => T("That answer doesn't match. Please try again.",
                                              "उत्तर जुळत नाही. कृपया पुन्हा प्रयत्न करा.",
                                              "यह उत्तर मेल नहीं खाता। कृपया फिर से प्रयास करें।",
                                              "એ જવાબ મેળ ખાતો નથી. કૃપા કરીને ફરી પ્રયાસ કરો.");
    public string ForgotDoneTitle         => T("Password updated",        "पासवर्ड अद्यतनित",             "पासवर्ड अपडेट किया गया",       "પાસવર્ડ અપડેટ થયો");
    public string ForgotDoneSubtitle      => T("Signing you back to the login screen…",
                                              "साइन इन स्क्रीनवर परत नेत आहे…",
                                              "साइन इन स्क्रीन पर वापस ले जा रहे हैं…",
                                              "સાઇન ઇન સ્ક્રીન પર પાછા લઈ જઈ રહ્યા છીએ…");
    public string PasswordResetSuccessBanner => T("Password reset successful. Sign in with your new password.",
                                                 "पासवर्ड रीसेट यशस्वी. नवीन पासवर्डसह साइन इन करा.",
                                                 "पासवर्ड रीसेट सफल। नए पासवर्ड से साइन इन करें।",
                                                 "પાસવર્ડ રીસેટ સફળ. નવા પાસવર્ડ સાથે સાઇન ઇન કરો.");

    // Settings-card strings for the recovery question row.
    public string RecoveryQuestionSectionLabel => T("PASSWORD RECOVERY",  "पासवर्ड रिकव्हरी",             "पासवर्ड रिकवरी",              "પાસવર્ડ રિકવરી");
    public string RecoveryOnStatusFormat  => T("Recovery question set: \"{0}\"",
                                              "रिकव्हरी प्रश्न सेट: \"{0}\"",
                                              "रिकवरी प्रश्न सेट: \"{0}\"",
                                              "રિકવરી પ્રશ્ન સેટ: \"{0}\"");
    public string RecoveryOffStatus       => T("Not set — you won't be able to reset your own password if you forget it.",
                                              "सेट नाही — विसरल्यास स्वतःचा पासवर्ड रीसेट करता येणार नाही.",
                                              "सेट नहीं — भूलने पर आप अपना पासवर्ड खुद रीसेट नहीं कर पाएंगे।",
                                              "સેટ નથી — ભૂલી જાવ તો તમારો પાસવર્ડ જાતે રીસેટ કરી શકશો નહીં.");
    public string RecoveryConfigureTitle  => T("Recovery question",       "रिकव्हरी प्रश्न",              "रिकवरी प्रश्न",                "રિકવરી પ્રશ્ન");
    public string RecoveryConfigureQuestionPrompt => T("Choose a question only you can answer. Leave blank to remove.",
                                                     "फक्त तुम्हीच उत्तर देऊ शकाल असा प्रश्न निवडा. काढून टाकण्यासाठी रिकामे ठेवा.",
                                                     "ऐसा प्रश्न चुनें जिसका उत्तर केवल आप जानते हों। हटाने के लिए खाली छोड़ें।",
                                                     "એવો પ્રશ્ન પસંદ કરો જેનો જવાબ ફક્ત તમે જ જાણો છો. કાઢી નાખવા ખાલી છોડો.");
    public string RecoveryConfigureAnswerPromptFormat => T("Answer for \"{0}\"",
                                                          "\"{0}\" चे उत्तर",
                                                          "\"{0}\" का उत्तर",
                                                          "\"{0}\" નો જવાબ");
    public string RecoveryQuestionSuggestionPh => T("e.g. Which city was the shop opened in?",
                                                   "उदा. दुकान कोणत्या शहरात उघडले?",
                                                   "उदा. दुकान किस शहर में खोली गई थी?",
                                                   "દા.ત. દુકાન કયા શહેરમાં ખોલી હતી?");
    public string RecoveryAnswerPh        => T("Your answer",             "तुमचे उत्तर",                  "आपका उत्तर",                  "તમારો જવાબ");
    public string RecoverySavedMsg        => T("Recovery question saved. You can now reset your password from the login screen if you ever forget it.",
                                              "रिकव्हरी प्रश्न जतन झाला. विसरल्यास आता तुम्ही साइन इन स्क्रीनवरून पासवर्ड रीसेट करू शकता.",
                                              "रिकवरी प्रश्न सहेजा गया। अब भूलने पर आप साइन इन स्क्रीन से पासवर्ड रीसेट कर सकते हैं।",
                                              "રિકવરી પ્રશ્ન સાચવ્યો. હવે ભૂલી જાવ તો સાઇન ઇન સ્ક્રીનથી પાસવર્ડ રીસેટ કરી શકો છો.");
    public string RecoveryClearedMsg      => T("Recovery question removed. Self-service reset is now disabled for this account.",
                                              "रिकव्हरी प्रश्न काढून टाकला. या खात्यासाठी स्वतःहून रीसेट आता बंद आहे.",
                                              "रिकवरी प्रश्न हटा दिया गया। इस खाते के लिए स्व-रीसेट अब बंद है।",
                                              "રિકવરી પ્રશ્ન કાઢી નાખ્યો. આ ખાતા માટે સ્વ-રીસેટ હવે બંધ છે.");

    // ── Quick-contact pills (Customer Detail header) ───────────────────────
    public string CallLabel               => T("Call",                   "कॉल करा",                     "कॉल करें",                    "કૉલ કરો");
    public string ChatLabel               => T("Chat",                   "चॅट करा",                     "चैट करें",                    "ચેટ કરો");
    public string NoPhoneTitle            => T("No phone number",        "फोन नंबर नाही",                "फ़ोन नंबर नहीं",              "ફોન નંબર નથી");
    public string NoPhoneMsg              => T("Add a phone number to this customer to enable quick contact.",
                                              "जलद संपर्कासाठी या ग्राहकाला फोन नंबर जोडा.",
                                              "त्वरित संपर्क के लिए इस ग्राहक का फ़ोन नंबर जोड़ें।",
                                              "ઝડપી સંપર્ક માટે આ ગ્રાહકનો ફોન નંબર ઉમેરો.");

    // ── Backup-stale banner (Home) ─────────────────────────────────────────
    public string BackupNeverMsg          => T(
        "Your data has never been backed up. Create a backup so you don't lose customer records.",
        "आपला डेटा अद्याप बॅकअप घेतलेला नाही. ग्राहकांची माहिती गमावू नये म्हणून बॅकअप घ्या.",
        "आपका डेटा अभी तक बैकअप नहीं हुआ है। ग्राहकों की जानकारी न खोने के लिए बैकअप लें।",
        "તમારો ડેટા હજી બૅકઅપ થયો નથી. ગ્રાહકોની માહિતી ગુમાવાય નહીં તે માટે બૅકઅપ લો.");
    public string BackupStaleDaysMsgFormat => T(
        "Last backup was {0} days ago. Take a fresh backup to stay safe.",
        "शेवटचा बॅकअप {0} दिवसांपूर्वी होता. सुरक्षित राहण्यासाठी नवीन बॅकअप घ्या.",
        "पिछला बैकअप {0} दिन पहले था। सुरक्षित रहने के लिए नया बैकअप लें।",
        "છેલ્લો બૅકઅપ {0} દિવસ પહેલાં હતો. સુરક્ષિત રહેવા માટે નવો બૅકઅપ લો.");
    // Note: BackupNowLabel is defined further down in the "Backup / Restore
    // (Phase 6)" section — reused here so the banner button matches the
    // Backup Manager's primary CTA in every language.
    public string DismissLabel            => T("Dismiss",              "बंद करा",                     "बंद करें",                    "બંધ કરો");

    // ── Text size (accessibility) ──────────────────────────────────────────
    // Kept out of the T() helper because these are already language-neutral —
    // "A⁻ / A / A⁺" reads the same in every supported script. Exposed as
    // constants purely so bindings can pick them up if we ever want to
    // localize the label itself.
    public string TextSizeTooltip         => T("Text size",              "फॉन्ट आकार",                  "टेक्स्ट आकार",                "ટેક્સ્ટ કદ");
    public string TextSizeHint            => T(
        "Pick a size that's comfortable to read. This applies to every screen.",
        "वाचायला सोयीस्कर आकार निवडा. हे प्रत्येक स्क्रीनवर लागू होते.",
        "पढ़ने में आरामदायक आकार चुनें। यह हर स्क्रीन पर लागू होगा।",
        "વાંચવામાં આરામદાયક કદ પસંદ કરો. આ દરેક સ્ક્રીન પર લાગુ પડે છે.");

    // ── Voice dictation on the measurement form ────────────────────────────
    public string SpeakLabel              => T("Speak",                  "बोला",                          "बोलें",                       "બોલો");
    public string SpeakHint               => T(
        "Tap the mic then read the measurements out loud (e.g. \"chest 32 waist 30\").",
        "माइक दाबा आणि मोजमाप मोठ्याने वाचा (उदा. \"चेस्ट 32 वेस्ट 30\").",
        "माइक दबाएं और माप ज़ोर से पढ़ें (जैसे \"चेस्ट 32 वेस्ट 30\").",
        "માઇક દબાવો અને માપ મોટેથી વાંચો (દા.ત. \"ચેસ્ટ 32 વેસ્ટ 30\").");
    public string VoiceUnavailableTitle   => T("Voice not available",    "आवाज उपलब्ध नाही",             "आवाज़ उपलब्ध नहीं",           "વૉઇસ ઉપલબ્ધ નથી");
    public string VoiceUnavailableMsg     => T(
        "Speech recognition isn't set up on this device. Enable Windows Speech in Settings > Privacy > Speech, or use the keypad instead.",
        "या डिव्हाइसवर आवाज ओळख सेट केलेली नाही. Settings > Privacy > Speech मध्ये Windows Speech सक्षम करा किंवा कीपॅड वापरा.",
        "इस डिवाइस पर वाणी पहचान सेट अप नहीं है। Settings > Privacy > Speech में Windows Speech सक्षम करें या कीपैड का उपयोग करें।",
        "આ ડિવાઇસ પર સ્પીચ રિકગ્નિશન સેટ થયું નથી. Settings > Privacy > Speech માં Windows Speech ચાલુ કરો અથવા કીપેડ વાપરો.");
    public string VoiceNothingFoundTitle  => T("No numbers heard",       "संख्या ऐकू आली नाही",           "संख्याएं नहीं सुनी",          "કોઈ સંખ્યા સંભળાઈ નથી");
    public string VoiceNothingFoundMsgFormat => T(
        "We heard: \"{0}\". Try again saying a field name and a number, e.g. \"chest 32\".",
        "आम्ही ऐकले: \"{0}\". फिल्ड आणि संख्या बोला, जसे \"चेस्ट 32\".",
        "हमने सुना: \"{0}\". फ़ील्ड और संख्या कहें, जैसे \"चेस्ट 32\".",
        "અમે સાંભળ્યું: \"{0}\". ફીલ્ડ અને સંખ્યા બોલો, જેમ કે \"ચેસ્ટ 32\".");

    // ── Employee dashboard ──────────────────────────────────────────────────
    public string MyWorkTitle             => T("My Work",                "माझे काम",                    "मेरा काम",                    "મારું કામ");
    public string MyWorkSubtitle          => T("Orders assigned to you", "तुम्हाला नियुक्त केलेले ऑर्डर",  "आपको सौंपे गए ऑर्डर",         "તમને સોંપાયેલા ઓર્ડર");
    public string NoAssignedWorkTitle     => T("Nothing assigned yet",   "अजून काही नियुक्त नाही",       "अभी तक कुछ नहीं सौंपा गया",   "હજી કંઈ સોંપાયું નથી");
    public string NoAssignedWorkHint      => T("Your admin will assign orders to you. Check back later.",
                                              "तुमचे प्रशासक तुम्हाला ऑर्डर नियुक्त करतील. नंतर पुन्हा तपासा.",
                                              "आपके व्यवस्थापक आपको ऑर्डर सौंपेंगे। बाद में देखें।",
                                              "તમારા એડમિન તમને ઓર્ડર સોંપશે. પછી ફરી તપાસો.");

    // ── Employee management ─────────────────────────────────────────────────
    public string EmployeesLabel          => T("Employees",              "कर्मचारी",                    "कर्मचारी",                    "કર્મચારીઓ");
    public string TeamLabel               => T("Team",                   "टीम",                          "टीम",                         "ટીમ");
    public string AddEmployeeLabel        => T("Add Employee",           "कर्मचारी जोडा",               "कर्मचारी जोड़ें",             "કર્મચારી ઉમેરો");
    public string EditEmployeeLabel       => T("Edit Employee",          "कर्मचारी संपादित करा",         "कर्मचारी संपादित करें",       "કર્મચારીને સંપાદિત કરો");
    public string ResetPasswordLabel      => T("Reset Password",         "पासवर्ड रीसेट करा",            "पासवर्ड रीसेट करें",          "પાસવર્ડ રીસેટ કરો");
    public string ResetPasswordShortLabel => T("Reset",                  "रीसेट",                        "रीसेट",                       "રીસેટ");
    public string SetPasswordLabel        => T("Set Password",           "पासवर्ड सेट करा",              "पासवर्ड सेट करें",            "પાસવર્ડ સેટ કરો");
    public string NewPasswordLabel        => T("New Password *",         "नवीन पासवर्ड *",              "नया पासवर्ड *",               "નવો પાસવર્ડ *");
    public string DeactivateLabel         => T("Deactivate",             "निष्क्रिय करा",                "निष्क्रिय करें",              "નિષ્ક્રિય કરો");
    public string ActivateLabel           => T("Activate",               "सक्रिय करा",                   "सक्रिय करें",                 "સક્રિય કરો");
    public string SaveButtonLabel         => T("Save",                   "जतन करा",                      "सहेजें",                      "સાચવો");
    public string ActiveLabel             => T("Active",                 "सक्रिय",                       "सक्रिय",                      "સક્રિય");
    public string InactiveLabel           => T("Inactive",               "निष्क्रिय",                    "निष्क्रिय",                   "નિષ્ક્રિય");
    public string RoleLabel               => T("Role",                   "भूमिका",                       "भूमिका",                      "ભૂમિકા");
    public string EmployeeSectionLabel    => T("EMPLOYEE",               "कर्मचारी",                    "कर्मचारी",                    "કર્મચારી");
    public string PermissionsSectionLabel => T("PERMISSIONS",            "परवानग्या",                    "अनुमतियाँ",                   "પરવાનગીઓ");
    public string PermissionAdminHint     => T("Full access to customers, orders, employees, backups, and exports.",
                                              "ग्राहक, ऑर्डर, कर्मचारी, बॅकअप आणि निर्यातांना पूर्ण प्रवेश.",
                                              "ग्राहकों, ऑर्डर, कर्मचारियों, बैकअप और निर्यात तक पूर्ण पहुँच।",
                                              "ગ્રાહકો, ઓર્ડર, કર્મચારીઓ, બેકઅપ અને નિકાસ પર સંપૂર્ણ પ્રવેશ.");
    public string PermissionEmployeeHint  => T("Can only view customers and mark order status as complete.",
                                              "फक्त ग्राहक पाहू शकतो आणि ऑर्डरची स्थिती पूर्ण म्हणून चिन्हांकित करू शकतो.",
                                              "केवल ग्राहकों को देख सकते हैं और ऑर्डर स्थिति पूर्ण के रूप में चिह्नित कर सकते हैं।",
                                              "માત્ર ગ્રાહકોને જોઈ શકે અને ઓર્ડરની સ્થિતિ પૂર્ણ તરીકે ચિહ્નિત કરી શકે.");
    public string NoEmployeesTitle        => T("No employees yet",       "अजून कोणी कर्मचारी नाहीत",     "अभी तक कोई कर्मचारी नहीं",     "હજી કોઈ કર્મચારી નથી");
    public string NoEmployeesHint         => T("Add teammates so you can assign work to them.",
                                              "काम नियुक्त करण्यासाठी सहकाऱ्यांना जोडा.",
                                              "काम सौंपने के लिए टीम के सदस्यों को जोड़ें।",
                                              "તેઓને કામ સોંપી શકાય તે માટે સાથીઓને ઉમેરો.");
    public string ConfirmDeactivateTitle  => T("Deactivate this user?",  "हा वापरकर्ता निष्क्रिय करायचा?", "इस उपयोगकर्ता को निष्क्रिय करें?", "આ વપરાશકર્તાને નિષ્ક્રિય કરવો?");
    public string ConfirmDeactivateMsg    => T("They won't be able to sign in until you reactivate them. Their history is preserved.",
                                              "तुम्ही पुन्हा सक्रिय करेपर्यंत ते साइन इन करू शकणार नाहीत. त्यांचा इतिहास जपला जातो.",
                                              "आप उन्हें फिर से सक्रिय करने तक वे साइन इन नहीं कर सकेंगे। उनका इतिहास सुरक्षित रहता है।",
                                              "તમે તેમને ફરીથી સક્રિય કરો ત્યાં સુધી તેઓ સાઇન ઇન કરી શકશે નહીં. તેમનો ઇતિહાસ સચવાય છે.");
    public string YouLabel                => T("(you)",                  "(तुम्ही)",                     "(आप)",                        "(તમે)");

    // ── Order assignment (Phase 4) ──────────────────────────────────────────
    public string AssignLabel             => T("Assign",                 "नियुक्त करा",                  "सौंपें",                      "સોંપો");
    public string AssignOrderTitle        => T("Assign Work",            "काम नियुक्त करा",              "काम सौंपें",                  "કામ સોંપો");
    public string AssignOrderSubtitle     => T("Pick who's responsible for each stage",
                                              "प्रत्येक टप्प्यासाठी जबाबदार व्यक्ती निवडा",
                                              "प्रत्येक चरण के लिए ज़िम्मेदार व्यक्ति चुनें",
                                              "દરેક તબક્કા માટે જવાબદાર વ્યક્તિ પસંદ કરો");
    public string OrderLabel              => T("Order",                  "ऑर्डर",                        "ऑर्डर",                       "ઓર્ડર");
    public string StageLabel              => T("Stage",                  "टप्पा",                        "चरण",                         "તબક્કો");
    public string AssignedToLabel         => T("Assigned to",            "नियुक्त",                     "सौंपा गया",                   "સોંપાયેલ");
    public string UnassignedLabel         => T("Unassigned",             "नियुक्त नाही",                 "असाइन नहीं",                  "સોંપાયેલ નથી");
    public string PickEmployeeLabel       => T("— Pick employee —",      "— कर्मचारी निवडा —",           "— कर्मचारी चुनें —",           "— કર્મચારી પસંદ કરો —");
    public string SaveAssignmentsLabel    => T("Save Assignments",       "नियुक्त्या जतन करा",            "असाइनमेंट सहेजें",            "સોંપણીઓ સાચવો");
    public string NoActiveEmployeesTitle  => T("No active employees",    "कोणीही सक्रिय कर्मचारी नाहीत", "कोई सक्रिय कर्मचारी नहीं",    "કોઈ સક્રિય કર્મચારી નથી");
    public string NoActiveEmployeesHint   => T("Add or activate an employee before assigning work.",
                                              "काम नियुक्त करण्यापूर्वी कर्मचारी जोडा किंवा सक्रिय करा.",
                                              "काम सौंपने से पहले कर्मचारी जोड़ें या सक्रिय करें।",
                                              "કામ સોંપતા પહેલા કર્મચારીને ઉમેરો અથવા સક્રિય કરો.");

    // ── My Work / employee dashboard (Phase 4) ──────────────────────────────
    public string MarkCompleteLabel       => T("Mark Complete",          "पूर्ण म्हणून चिन्हांकित करा", "पूर्ण के रूप में चिह्नित करें", "પૂર્ણ તરીકે ચિહ્નિત કરો");
    public string CompleteShortLabel      => T("Done",                   "पूर्ण",                        "पूर्ण",                       "થયું");
    public string YourStageLabel          => T("Your stage",             "तुमचा टप्पा",                 "आपका चरण",                    "તમારો તબક્કો");
    public string ViewOrderLabel          => T("View",                   "पहा",                          "देखें",                       "જુઓ");
    public string DueLabel                => T("Due",                    "देय",                          "देय",                         "લેણું");
    public string NoDueDateLabel          => T("No due date",            "देय तारीख नाही",               "कोई देय तिथि नहीं",           "કોઈ લેણી તારીખ નથી");
    public string ConfirmMarkCompleteMsg  => T("Mark this stage complete? The order will advance to this stage.",
                                              "हा टप्पा पूर्ण म्हणून चिन्हांकित करायचा? ऑर्डर या टप्प्यावर जाईल.",
                                              "इस चरण को पूर्ण के रूप में चिह्नित करें? ऑर्डर इस चरण में आगे बढ़ जाएगा।",
                                              "આ તબક્કો પૂર્ણ તરીકે ચિહ્નિત કરવો? ઓર્ડર આ તબક્કામાં આગળ વધશે.");

    // ── Activity feed / audit (Phase 5) ─────────────────────────────────────
    public string ActivityLogTitle        => T("Activity Log",           "क्रियाकलाप नोंद",             "गतिविधि लॉग",                 "પ્રવૃત્તિ લોગ");
    public string ActivityLogSubtitle     => T("Who did what, and when", "कोणी काय आणि कधी केले",       "किसने क्या और कब किया",       "કોણે શું અને ક્યારે કર્યું");
    public string RecentActivityLabel     => T("Recent Activity",        "अलीकडील क्रियाकलाप",          "हाल की गतिविधि",              "તાજેતરની પ્રવૃત્તિ");
    public string NoActivityTitle         => T("Nothing here yet",       "अजून काही नाही",              "अभी तक कुछ नहीं",             "હજી કંઈ નથી");
    public string NoActivityHint          => T("Activity from you and your team will appear here.",
                                              "तुमच्या आणि टीमच्या क्रियाकलाप येथे दिसतील.",
                                              "आपकी और आपकी टीम की गतिविधियाँ यहाँ दिखाई देंगी।",
                                              "તમારી અને તમારી ટીમની પ્રવૃત્તિઓ અહીં દેખાશે.");
    public string FilterByActionLabel     => T("Filter",                 "फिल्टर",                      "फ़िल्टर",                     "ફિલ્ટર");
    public string FilterAllLabel          => T("All",                    "सर्व",                        "सभी",                         "બધા");
    public string FilterAuthLabel         => T("Sign-ins",               "साइन-इन्स",                   "साइन-इन",                     "સાઇન ઇન");
    public string FilterCustomersLabel    => T("Customers",              "ग्राहक",                     "ग्राहक",                      "ગ્રાહકો");
    public string FilterOrdersLabel       => T("Orders",                 "ऑर्डर्स",                     "ऑर्डर",                       "ઓર્ડર");
    public string FilterUsersLabel        => T("Team",                   "टीम",                          "टीम",                         "ટીમ");
    public string FilterAssignmentsLabel  => T("Assignments",            "नियुक्त्या",                  "असाइनमेंट",                   "સોંપણીઓ");
    public string TodayLabel              => T("Today",                  "आज",                           "आज",                          "આજે");
    public string YesterdayLabel          => T("Yesterday",              "काल",                          "कल",                          "ગઈકાલે");
    public string JustNowLabel            => T("just now",               "आत्ताच",                      "अभी अभी",                    "હમણાં જ");
    public string MinutesAgoFmt           => T("{0} min ago",            "{0} मि. पूर्वी",              "{0} मिनट पहले",               "{0} મિનિટ પહેલા");
    public string HoursAgoFmt             => T("{0} h ago",              "{0} तासापूर्वी",              "{0} घंटे पहले",               "{0} કલાક પહેલા");
    public string DaysAgoFmt              => T("{0} d ago",              "{0} दिवसांपूर्वी",            "{0} दिन पहले",                "{0} દિવસ પહેલા");

    // ── Backup / Restore (Phase 6) ──────────────────────────────────────────
    public string BackupManagerTitle      => T("Backup & Restore",       "बॅकअप आणि पुनर्संचयन",         "बैकअप और पुनर्स्थापित",       "બૅકઅપ અને પુનઃસ્થાપિત");
    public string BackupManagerSubtitle   => T("Protect your shop data", "आपल्या दुकानाचा डेटा सुरक्षित करा", "अपनी दुकान के डेटा को सुरक्षित रखें", "તમારી દુકાનનો ડેટા સુરક્ષિત રાખો");
    public string BackupNowLabel          => T("Backup Now",             "आत्ता बॅकअप घ्या",             "अभी बैकअप लें",               "હમણાં બૅકઅપ લો");
    public string RestoreFromFileLabel    => T("Restore from File…",     "फाइलमधून पुनर्संचयन…",         "फ़ाइल से पुनर्स्थापित…",      "ફાઇલમાંથી પુનઃસ્થાપિત…");
    public string LocalBackupsLabel       => T("Local Backups",          "स्थानिक बॅकअप्स",              "स्थानीय बैकअप",               "સ્થાનિક બૅકઅપ્સ");
    public string NoBackupsTitle          => T("No backups yet",         "अजून बॅकअप्स नाहीत",           "अभी तक कोई बैकअप नहीं",       "હજી કોઈ બૅકઅપ નથી");
    public string NoBackupsHint           => T("Tap 'Backup Now' to create your first archive.",
                                              "पहिली बॅकअप तयार करण्यासाठी 'आत्ता बॅकअप घ्या' दाबा.",
                                              "अपना पहला बैकअप बनाने के लिए 'अभी बैकअप लें' पर टैप करें।",
                                              "તમારો પ્રથમ બૅકઅપ બનાવવા માટે 'હમણાં બૅકઅપ લો' પર ટૅપ કરો.");
    public string EncryptedBadge          => T("Encrypted",              "एन्क्रिप्टेड",                  "एन्क्रिप्टेड",                 "એન્ક્રિપ્ટેડ");
    public string PlainBadge              => T("Plain",                  "साधे",                          "प्लेन",                       "સાદું");
    public string AutoBadge               => T("Auto",                   "स्वयं",                         "स्वतः",                       "ઓટો");
    public string RestoreThisLabel        => T("Restore This",           "हे पुनर्संचयित करा",            "इसे पुनर्स्थापित करें",       "આ પુનઃસ્થાપિત કરો");
    public string BackupSizeLabel         => T("Size",                   "आकार",                          "आकार",                        "કદ");
    public string OptionalPassphraseLabel => T("Encryption Passphrase (optional)",
                                              "एन्क्रिप्शन पासफ्रेज (पर्यायी)",
                                              "एन्क्रिप्शन पासफ्रेज़ (वैकल्पिक)",
                                              "એન્ક્રિપ્શન પાસફ્રેઝ (વૈકલ્પિક)");
    public string PassphraseLabel         => T("Passphrase",             "पासफ्रेज",                     "पासफ्रेज़",                   "પાસફ્રેઝ");
    public string PassphrasePh            => T("leave empty for unencrypted",
                                              "एन्क्रिप्शनसाठी रिकामे ठेवा",
                                              "बिना एन्क्रिप्शन के लिए खाली छोड़ें",
                                              "એન્ક્રિપ્શન વગર માટે ખાલી છોડો");
    public string PassphraseNeededTitle   => T("Passphrase required",    "पासफ्रेज आवश्यक",              "पासफ्रेज़ आवश्यक",            "પાસફ્રેઝ જરૂરી");
    public string PassphraseNeededMsg     => T("This backup is encrypted. Enter the passphrase to restore it.",
                                              "हा बॅकअप एन्क्रिप्टेड आहे. पुनर्संचयनासाठी पासफ्रेज टाका.",
                                              "यह बैकअप एन्क्रिप्टेड है। पुनर्स्थापित करने के लिए पासफ्रेज़ दर्ज करें।",
                                              "આ બૅકઅપ એન્ક્રિપ્ટેડ છે. પુનઃસ્થાપિત કરવા પાસફ્રેઝ દાખલ કરો.");
    public string WrongPassphraseMsg      => T("Could not decrypt. Check the passphrase and try again.",
                                              "डिक्रिप्ट करता आले नाही. पासफ्रेज तपासा आणि पुन्हा प्रयत्न करा.",
                                              "डिक्रिप्ट नहीं कर सका। पासफ्रेज़ जाँचें और फिर से प्रयास करें।",
                                              "ડિક્રિપ્ટ કરી શકાયું નથી. પાસફ્રેઝ તપાસો અને ફરી પ્રયાસ કરો.");
    public string BadFormatMsg            => T("This file is not a valid TapeTracker backup.",
                                              "ही फाइल वैध TapeTracker बॅकअप नाही.",
                                              "यह फ़ाइल एक वैध TapeTracker बैकअप नहीं है।",
                                              "આ ફાઇલ માન્ય TapeTracker બૅકઅપ નથી.");
    public string RestoreConfirmTitle     => T("Restore backup?",        "बॅकअप पुनर्संचयन?",             "बैकअप पुनर्स्थापित करें?",    "બૅકઅપ પુનઃસ્થાપિત કરવો?");
    public string RestoreConfirmMsg       => T("This REPLACES all current shop data with the backup. The app will restart. Continue?",
                                              "हे सर्व सध्याचा दुकान डेटा बॅकअपने बदलेल. अ‍ॅप पुन्हा सुरू होईल. सुरू ठेवायचे?",
                                              "यह सभी मौजूदा दुकान डेटा को बैकअप से बदल देगा। ऐप पुनरारंभ होगा। जारी रखें?",
                                              "આ બધા વર્તમાન દુકાન ડેટાને બૅકઅપથી બદલશે. એપ પુનઃશરૂ થશે. ચાલુ રાખવું?");
    public string YesRestoreLabel         => T("Yes, restore",           "होय, पुनर्संचयन",              "हाँ, पुनर्स्थापित करें",      "હા, પુનઃસ્થાપિત કરો");
    public string RestoredSuccessTitle    => T("Restored",                "पुनर्संचयित",                  "पुनर्स्थापित",                "પુનઃસ્થાપિત");
    public string RestoredSuccessMsg      => T("Backup restored. Please restart the app to load the new data.",
                                              "बॅकअप पुनर्संचयित. नवीन डेटा लोड करण्यासाठी अ‍ॅप पुन्हा सुरू करा.",
                                              "बैकअप पुनर्स्थापित हुआ। नया डेटा लोड करने के लिए ऐप पुनरारंभ करें।",
                                              "બૅકઅપ પુનઃસ્થાપિત. નવો ડેટા લોડ કરવા એપ ફરી શરૂ કરો.");
    public string BackupCreatedFmt        => T("Backup saved: {0}",      "बॅकअप जतन: {0}",               "बैकअप सहेजा गया: {0}",        "બૅકઅપ સાચવ્યો: {0}");
    public string ConfirmDeleteBackupMsg  => T("Delete this backup file? This cannot be undone.",
                                              "ही बॅकअप फाइल हटवायची? हे पूर्ववत करता येणार नाही.",
                                              "इस बैकअप फ़ाइल को हटाएं? इसे पूर्ववत नहीं किया जा सकता।",
                                              "આ બૅકઅપ ફાઇલ કાઢી નાખવી? આને પૂર્વવત્ કરી શકાતું નથી.");

    // ── Auto-backup settings (Phase 6) ──────────────────────────────────────
    public string AutoBackupSectionLabel  => T("AUTO-BACKUP",            "स्वयं-बॅकअप",                  "स्वतः-बैकअप",                 "ઓટો-બૅકઅપ");
    public string AutoBackupEnabledLabel  => T("Enabled",                "सक्षम",                         "सक्षम",                       "સક્ષમ");
    public string AutoBackupEnabledHint   => T("Silently back up every few days when you sign in.",
                                              "साइन-इन झाल्यावर काही दिवसांच्या अंतराने शांतपणे बॅकअप घ्या.",
                                              "साइन-इन करने पर हर कुछ दिनों में चुपचाप बैकअप लें।",
                                              "સાઇન ઇન થાય ત્યારે થોડા દિવસના અંતરે શાંતિથી બૅકઅપ લો.");
    public string IntervalDaysLabel       => T("Interval (days)",        "अंतराल (दिवस)",                "अंतराल (दिन)",                "અંતરાલ (દિવસ)");
    public string RetentionLabel          => T("Keep last N auto-backups", "शेवटच्या N स्वयं-बॅकअप्स ठेवा", "अंतिम N स्वतः-बैकअप रखें", "છેલ્લા N ઓટો-બૅકઅપ્સ રાખો");
    public string LastAutoBackupLabel     => T("Last auto-backup",       "शेवटचा स्वयं-बॅकअप",           "अंतिम स्वतः-बैकअप",           "છેલ્લો ઓટો-બૅકઅપ");
    public string NeverLabel              => T("Never",                  "कधीच नाही",                    "कभी नहीं",                    "ક્યારેય નહીં");
    // Reused by both the Shop Settings summary tile and the auto-backup card
    // header — "Never" is the natural "no runs yet" state so a dedicated
    // AutoBackupNeverLabel would only diverge translations pointlessly.
    public string AutoBackupNeverLabel    => NeverLabel;
    public string AutoBackupOffSummary    => T(
        "Off · turn on to protect your data automatically.",
        "बंद · तुमचा डेटा आपोआप सुरक्षित ठेवण्यासाठी चालू करा.",
        "बंद · अपने डेटा को स्वचालित रूप से सुरक्षित रखने के लिए चालू करें।",
        "બંધ · તમારો ડેટા ઓટોમેટિકલી સુરક્ષિત રાખવા ચાલુ કરો.");
    public string AutoBackupOnSummaryFormat => T(
        "On · every {0} days · last: {1}",
        "चालू · दर {0} दिवसांनी · शेवटचा: {1}",
        "चालू · हर {0} दिन · अंतिम: {1}",
        "ચાલુ · દર {0} દિવસે · છેલ્લો: {1}");
    public string OpenBackupManagerLabel  => T("Manage…",                "व्यवस्थापित करा…",             "प्रबंधित करें…",              "મેનેજ કરો…");
    public string AutoBackupPassphraseLabel => T("Auto-backup passphrase",
                                                 "स्वयं-बॅकअप पासफ्रेज",
                                                 "स्वतः-बैकअप पासफ्रेज़",
                                                 "ઓટો-બૅકઅપ પાસફ્રેઝ");
    public string AutoBackupPassphraseHint  => T("Encrypts scheduled backups. Store this passphrase somewhere safe — without it, restore is not possible.",
                                                 "अनुसूचित बॅकअप्स एन्क्रिप्ट करते. हा पासफ्रेज सुरक्षित ठिकाणी ठेवा — त्याशिवाय पुनर्संचयन शक्य नाही.",
                                                 "अनुसूचित बैकअप को एन्क्रिप्ट करता है। इस पासफ्रेज़ को सुरक्षित स्थान पर रखें — इसके बिना पुनर्स्थापित नहीं किया जा सकता।",
                                                 "શેડ્યુલ કરેલ બૅકઅપ્સ એન્ક્રિપ્ટ કરે છે. આ પાસફ્રેઝ સુરક્ષિત જગ્યાએ રાખો — તેના વગર પુનઃસ્થાપિત શક્ય નથી.");
    public string SavedLabel              => T("Saved",                  "जतन केले",                     "सहेजा गया",                   "સાચવ્યું");
    public string ClearPassphraseLabel    => T("Remove Passphrase",      "पासफ्रेज काढा",                "पासफ्रेज़ हटाएं",             "પાસફ્રેઝ હટાવો");

    // ── Delivery calendar / Rush / SLA (Phase 7) ────────────────────────────
    public string DeliveryCalendarLabel   => T("Delivery Calendar",      "वितरण कॅलेंडर",                "डिलीवरी कैलेंडर",             "ડિલિવરી કૅલેન્ડર");
    public string RushLabel               => T("Rush",                   "तात्काळ",                       "तत्काल",                      "તાત્કાલિક");
    public string RushOrderLabel          => T("Rush order",             "तात्काळ ऑर्डर",                "तत्काल ऑर्डर",                "તાત્કાલિક ઓર્ડર");
    public string MarkAsRushLabel         => T("Mark as rush",           "तात्काळ म्हणून चिन्हांकित करा",  "तत्काल के रूप में चिह्नित करें", "તાત્કાલિક તરીકે ચિહ્નિત કરો");
    public string RushHint                => T("Highlight this order in red across queues and calendars.",
                                              "हे ऑर्डर सर्व यादीत आणि कॅलेंडरमध्ये लाल रंगात दाखवा.",
                                              "इस ऑर्डर को सभी सूचियों और कैलेंडर में लाल रंग में हाइलाइट करें।",
                                              "આ ઓર્ડરને બધી યાદીઓ અને કૅલેન્ડરમાં લાલ રંગમાં હાઇલાઇટ કરો.");
    public string StuckDaysFormat         => T("Stuck {0}d",              "{0} दिवसांपासून अटकले",         "{0} दिन से अटका",              "{0} દિવસથી અટવાયું");
    // OverdueLabel already defined earlier in this file (dashboard stat).
    public string DueTodayLabel           => T("Due today",               "आज ड्यू",                      "आज देय",                      "આજે ડ્યુ");
    // Format string used by the Home widget's per-order chip; the {0} is a
    // positive day count. Kept plural in every language for simplicity —
    // "1 day late" and "3 days late" both read naturally in scripts that
    // don't inflect for count.
    public string DaysOverdueFormat       => T(
        "{0}d late",
        "{0} दि. उशीर",
        "{0} दि. देर",
        "{0} દિ. મોડું");
    public string UrgentOrdersTitle       => T(
        "Needs attention",
        "लक्ष द्या",
        "ध्यान दें",
        "ધ્યાન આપો");
    public string DueTomorrowLabel        => T("Due tomorrow",            "उद्या ड्यू",                    "कल देय",                      "કાલે ડ્યુ");
    public string InNDaysFormat           => T("In {0}d",                 "{0} दिवसांत",                   "{0} दिनों में",                "{0} દિવસમાં");

    // Dashboard "Attention needed" widget
    public string AttentionNeededLabel    => T("ATTENTION NEEDED",        "लक्ष द्या",                     "ध्यान चाहिए",                 "ધ્યાન જોઈએ");
    public string NoStuckOrdersLabel      => T("Nothing stuck. Great work!",
                                              "काहीही अटकलेले नाही. उत्तम काम!",
                                              "कुछ भी अटका नहीं। शानदार!",
                                              "કંઈ અટવાયું નથી. શાનદાર!");

    // Delivery calendar page
    public string CalendarTodayLabel      => T("Today",                   "आज",                            "आज",                          "આજે");
    public string CalendarPrevMonthLabel  => T("Previous month",          "मागील महिना",                   "पिछला महीना",                 "પાછલો મહિનો");
    public string CalendarNextMonthLabel  => T("Next month",              "पुढील महिना",                   "अगला महीना",                  "આગલો મહિનો");
    public string NoDeliveriesLabel       => T("No deliveries this month.",
                                              "या महिन्यात कोणतेही वितरण नाही.",
                                              "इस महीने कोई डिलीवरी नहीं।",
                                              "આ મહિનામાં કોઈ ડિલિવરી નથી.");
    public string MonthlyDeliveriesFormat => T("{0} deliveries this month",
                                              "या महिन्यात {0} वितरण",
                                              "इस महीने {0} डिलीवरी",
                                              "આ મહિનામાં {0} ડિલિવરી");
    public string CalendarLegendLabel     => T("Legend",                  "चिन्हे",                        "सूचक",                        "સૂચક");
    public string LegendOnTrackLabel      => T("On track",                "योग्य मार्गावर",                "सही रास्ते पर",                "યોગ્ય માર્ગ પર");
    public string LegendDeliveredLabel    => T("Delivered",               "वितरित",                        "वितरित",                      "વિતરિત");

    // ── Shop settings (Phase 8) ──────────────────────────────────────────────
    public string ShopSettingsLabel       => T("Shop Settings",           "दुकान सेटिंग्ज",                 "दुकान सेटिंग्स",              "દુકાન સેટિંગ્સ");
    public string ShopIdentitySection     => T("SHOP IDENTITY",           "दुकान ओळख",                     "दुकान पहचान",                 "દુકાન ઓળખ");
    // ShopNameLabel + ShopAddressLabel are already defined earlier in this
    // file (in the org-registration section). Reusing keeps translations
    // consistent between the registration and settings pages.
    public string OwnerNameLabel          => T("Owner / proprietor",      "मालक",                          "मालिक",                       "માલિક");
    public string ShopEmailLabel          => T("Email",                   "ईमेल",                          "ईमेल",                        "ઈમેલ");
    public string BillingSection          => T("BILLING",                 "बिलिंग",                        "बिलिंग",                      "બિલિંગ");
    public string GstNumberLabel          => T("GST / VAT number",        "जीएसटी क्रमांक",                 "जीएसटी संख्या",               "જીએસટી નંબર");
    public string CurrencySymbolLabel     => T("Currency symbol",         "चलन चिन्ह",                     "मुद्रा चिह्न",                "ચલણ ચિહ્ન");
    public string TaxRateLabel            => T("Default tax rate (%)",    "मूलभूत कर दर (%)",              "डिफ़ॉल्ट कर दर (%)",           "ડિફોલ્ટ ટૅક્સ દર (%)");
    public string InvoicePrefixLabel      => T("Invoice number prefix",   "इनव्हॉइस उपसर्ग",              "इनवॉइस प्रीफ़िक्स",            "ઇનવોઇસ પ્રીફિક્સ");
    public string LogoLabel               => T("Shop logo",               "दुकानाचा लोगो",                  "दुकान का लोगो",               "દુકાનનો લોગો");
    public string ChooseLogoLabel         => T("Choose image…",           "प्रतिमा निवडा…",                 "छवि चुनें…",                  "છબી પસંદ કરો…");
    public string RemoveLogoLabel         => T("Remove logo",             "लोगो काढा",                      "लोगो हटाएं",                  "લોગો હટાવો");
    public string SaveSettingsLabel       => T("Save settings",           "सेटिंग्ज जतन करा",              "सेटिंग्स सहेजें",             "સેટિંગ્સ સાચવો");
    public string SettingsSavedTitle      => T("Settings saved",          "सेटिंग्ज जतन झाल्या",          "सेटिंग्स सहेजी गईं",           "સેટિંગ્સ સાચવ્યાં");
    public string SettingsSavedMsg        => T("Your shop details will appear on invoices and messages.",
                                              "तुमचे तपशील इनव्हॉइस आणि संदेशांवर दिसतील.",
                                              "आपका विवरण इनवॉइस और संदेशों पर दिखेगा।",
                                              "તમારા ડિટેલ્સ ઇનવોઇસ અને સંદેશાઓ પર દેખાશે.");

    // ── Pricing & payments (Phase 8) ─────────────────────────────────────────
    public string PricingSection          => T("PRICING & PAYMENT",       "किंमत आणि पेमेंट",              "मूल्य और भुगतान",             "કિંમત અને પેમેન્ટ");
    public string StitchingChargeLabel    => T("Stitching charge",        "शिवण शुल्क",                    "सिलाई शुल्क",                 "સિલાઈ ચાર્જ");
    public string DiscountLabel           => T("Discount",                "सूट",                           "छूट",                         "ડિસ્કાઉન્ટ");
    public string TaxLabel                => T("Tax",                     "कर",                            "कर",                          "ટૅક્સ");
    public string SubtotalLabel           => T("Subtotal",                "उप-योग",                        "उप-योग",                      "સબટોટલ");
    public string GrandTotalLabel         => T("Grand total",             "एकूण",                          "कुल योग",                     "કુલ સરવાળો");
    public string AdvancePaidLabel        => T("Advance paid",            "पूर्व-दिलेला",                   "अग्रिम भुगतान",              "એડવાન્સ ચૂકવેલ");
    public string BalanceDueLabel         => T("Balance due",             "बाकी",                          "बकाया",                       "બાકી");
    public string PaymentMethodLabel      => T("Payment method",          "पेमेंट पद्धत",                  "भुगतान विधि",                 "પેમેન્ટ મેથડ");
    public string PaidInFullLabel         => T("Paid",                    "पूर्ण भरले",                    "पूरा भुगतान",                 "પૂરું ચૂકવેલ");
    public string PartialPaidLabel        => T("Partial",                 "आंशिक",                         "आंशिक",                       "આંશિક");
    public string UnpaidLabel             => T("Unpaid",                  "अनदेय",                         "अवैतनिक",                     "અવેતન");
    public string NoPricingLabel          => T("No price set",            "किंमत नाही",                    "मूल्य नहीं",                   "કિંમત નહીં");

    // Payment method enum labels
    public string PaymentNoneLabel        => T("Not paid",                "पैसे नाहीत",                    "भुगतान नहीं",                 "ચૂકવેલ નહીં");
    public string PaymentCashLabel        => T("Cash",                    "रोख",                            "नकद",                         "રોકડ");
    public string PaymentUpiLabel         => T("UPI",                     "यूपीआय",                        "यूपीआई",                      "યુપીઆઈ");
    public string PaymentCardLabel        => T("Card",                    "कार्ड",                         "कार्ड",                       "કાર્ડ");
    public string PaymentBankTransferLabel=> T("Bank transfer",           "बँक ट्रान्सफर",                  "बैंक ट्रांसफर",              "બેંક ટ્રાન્સફર");
    public string PaymentOtherLabel       => T("Other",                   "इतर",                           "अन्य",                        "અન્ય");

    // Dashboard outstanding
    public string OutstandingLabel        => T("Outstanding",             "बाकी रक्कम",                    "बकाया राशि",                  "બાકી રકમ");
    public string RevenueThisMonthLabel   => T("Revenue this month",      "या महिन्याची कमाई",             "इस महीने की आय",              "આ મહિનાની આવક");

    // Invoice PDF labels
    public string InvoiceLabel            => T("INVOICE",                 "इनव्हॉइस",                      "इनवॉइस",                      "ઇનવોઇસ");
    public string InvoiceNumberLabel      => T("Invoice #",               "इनव्हॉइस क्र.",                 "इनवॉइस #",                    "ઇનવોઇસ #");
    public string BillToLabel             => T("Bill to",                 "बिल-टू",                        "बिल टू",                      "બિલ ટૂ");
    public string ThankYouLabel           => T("Thank you for your business!",
                                              "आपल्या व्यवसायाबद्दल धन्यवाद!",
                                              "आपके व्यवसाय के लिए धन्यवाद!",
                                              "તમારા વ્યવસાય માટે આભાર!");
    public string GeneratedByLabel        => T("Generated by TapeTracker",  "TapeTracker द्वारे तयार केले",     "TapeTracker द्वारा जनरेट",       "TapeTracker દ્વારા બનાવેલ");
    public string PaidLabel               => T("PAID",                    "भरले",                          "भुगतान",                      "ચૂકવેલ");

    // WhatsApp message templates (Phase 8)
    public string WaReadyMessageFormat    => T(
        "Dear {0}, your order #{1} is ready for collection at {2}. Balance due: {3}{4}. Please visit us at your convenience.",
        "प्रिय {0}, तुमची ऑर्डर #{1} {2} येथे तयार आहे. बाकी: {3}{4}. सोयीनुसार भेट द्या.",
        "प्रिय {0}, आपका ऑर्डर #{1} {2} पर तैयार है। बकाया: {3}{4}। सुविधानुसार आइए।",
        "પ્રિય {0}, તમારો ઓર્ડર #{1} {2} પર તૈયાર છે. બાકી: {3}{4}. અનુકૂળ સમયે આવો.");
    public string WaReminderMessageFormat => T(
        "Dear {0}, this is a friendly reminder that your order #{1} at {2} has a pending balance of {3}{4}. Please clear the balance at your convenience. Thank you!",
        "प्रिय {0}, {2} येथील तुमच्या ऑर्डर #{1} वर {3}{4} बाकी आहे. सोयीनुसार भरा. धन्यवाद!",
        "प्रिय {0}, {2} पर आपके ऑर्डर #{1} पर {3}{4} बकाया है। सुविधानुसार भुगतान करें। धन्यवाद!",
        "પ્રિય {0}, {2} પર તમારા ઓર્ડર #{1} પર {3}{4} બાકી છે. અનુકૂળતાએ ચૂકવો. આભાર!");
    public string SendReminderLabel       => T("Send payment reminder",   "पेमेंट स्मरणपत्र पाठवा",         "भुगतान रिमाइंडर भेजें",       "પેમેન્ટ રિમાઇન્ડર મોકલો");

    // ── Invoice line items (Phase 9) ─────────────────────────────────────────
    public string LineItemsSection        => T("BILL ITEMS",              "बिल आयटम",                      "बिल आइटम",                   "બિલ આઇટમ્સ");
    public string AddLineItemLabel        => T("+ Add item",              "+ आयटम जोडा",                    "+ आइटम जोड़ें",              "+ આઇટમ ઉમેરો");
    public string RemoveLineItemLabel     => T("Remove",                  "काढा",                          "हटाएं",                       "હટાવો");
    public string LineItemForLabel        => T("For",                     "साठी",                          "के लिए",                      "માટે");
    public string LineItemForPlaceholder  => T("Person (optional)",       "व्यक्ती (पर्यायी)",              "व्यक्ति (वैकल्पिक)",           "વ્યક્તિ (વૈકલ્પિક)");
    public string LineItemDescLabel       => T("Item",                    "वस्तू",                         "आइटम",                        "આઇટમ");
    public string LineItemDescPlaceholder => T("Shirt, Pant, Kurta…",     "शर्ट, पॅन्ट, कुर्ता…",           "शर्ट, पैंट, कुर्ता…",           "શર્ટ, પેન્ટ, કુર્તા…");
    public string LineItemQtyLabel        => T("Qty",                     "प्रमाण",                        "मात्रा",                       "જથ્થો");
    public string LineItemRateLabel       => T("Rate",                    "दर",                            "दर",                          "દર");
    public string LineItemAmountLabel     => T("Amount",                  "रक्कम",                         "राशि",                         "રકમ");
    public string NoLineItemsLabel        => T("No items yet. Tap + to add one.",
                                              "अजून आयटम नाहीत. जोडण्यासाठी + दाबा.",
                                              "अभी कोई आइटम नहीं। जोड़ने के लिए + दबाएं।",
                                              "હજી કોઈ આઇટમ નથી. ઉમેરવા માટે + દબાવો.");

    // ── Invoice gating messages (Phase 9) ────────────────────────────────────
    public string InvoiceLockedTitle      => T("Invoice not available",   "इनव्हॉइस उपलब्ध नाही",           "इनवॉइस उपलब्ध नहीं",           "ઇનવોઇસ ઉપલબ્ધ નથી");
    public string InvoiceNotReadyMsg      => T("Invoices can only be generated once the order is marked Ready or Delivered.",
                                              "ऑर्डर 'तयार' किंवा 'दिली' म्हणून चिन्हांकित झाल्यावरच इनव्हॉइस तयार करता येईल.",
                                              "ऑर्डर को 'तैयार' या 'डिलीवर' चिह्नित करने के बाद ही इनवॉइस बनाया जा सकता है।",
                                              "ઓર્ડરને 'તૈયાર' અથવા 'ડિલિવર' ચિહ્નિત કર્યા પછી જ ઇનવોઇસ બનાવી શકાય છે.");
    public string InvoiceAdminOnlyMsg     => T("Only administrators can generate invoices.",
                                              "फक्त प्रशासकच इनव्हॉइस तयार करू शकतात.",
                                              "केवल व्यवस्थापक ही इनवॉइस बना सकते हैं।",
                                              "ફક્ત વ્યવસ્થાપકો જ ઇનવોઇસ બનાવી શકે છે.");
    public string GenerateInvoiceLabel    => T("Generate invoice",        "इनव्हॉइस तयार करा",              "इनवॉइस बनाएं",                "ઇનવોઇસ બનાવો");

    // ── Item catalog + family picker (Phase 9.1) ─────────────────────────────
    public string ItemCatalogSection      => T("ITEM CATALOG",            "आयटम कॅटलॉग",                   "आइटम कैटलॉग",                 "આઇટમ કૅટલૉગ");
    public string ItemCatalogHint         => T("Names shown as quick-pick chips on the bill items screen.",
                                              "बिल आयटम पडद्यावर क्विक-पिक चिन्हे म्हणून दाखवली जातात.",
                                              "बिल आइटम स्क्रीन पर क्विक-पिक चिप्स के रूप में दिखाए जाते हैं।",
                                              "બિલ આઇટમ સ્ક્રીન પર ક્વિક-પિક ચિપ્સ તરીકે બતાવવામાં આવે છે.");
    public string AddCatalogItemPh        => T("Type name and press +",   "नाव लिहा आणि + दाबा",           "नाम टाइप करें और + दबाएं",     "નામ ટાઇપ કરો અને + દબાવો");
    public string AddCatalogItemLabel     => T("Add",                     "जोडा",                          "जोड़ें",                       "ઉમેરો");
    public string CatalogEmptyHint        => T("No items yet — add your first garment name above.",
                                              "अजून आयटम नाहीत — वरून पहिले नाव जोडा.",
                                              "अभी कोई आइटम नहीं — ऊपर पहला नाम जोड़ें।",
                                              "હજી કોઈ આઇટમ નથી — ઉપર પ્રથમ નામ ઉમેરો.");
    public string PickFamilyMemberTitle   => T("Choose family member",    "कुटुंब सदस्य निवडा",             "पारिवारिक सदस्य चुनें",         "કુટુંબ સભ્ય પસંદ કરો");
    public string PickItemTitle           => T("Choose item",             "आयटम निवडा",                    "आइटम चुनें",                    "આઇટમ પસંદ કરો");
}

// ── Payment method localization helper ───────────────────────────────────────
public static class PaymentMethodExtensions
{
    /// <summary>Returns the current-language display name for a payment method.
    /// Kept as an extension so XAML and code-behind can both use it consistently.</summary>
    public static string Localize(this TapeTracker.Models.PaymentMethod method)
    {
        var L = LocalizationService.Current;
        return method switch
        {
            TapeTracker.Models.PaymentMethod.Cash         => L.PaymentCashLabel,
            TapeTracker.Models.PaymentMethod.Upi          => L.PaymentUpiLabel,
            TapeTracker.Models.PaymentMethod.Card         => L.PaymentCardLabel,
            TapeTracker.Models.PaymentMethod.BankTransfer => L.PaymentBankTransferLabel,
            TapeTracker.Models.PaymentMethod.Other        => L.PaymentOtherLabel,
            _                                           => L.PaymentNoneLabel
        };
    }
}
