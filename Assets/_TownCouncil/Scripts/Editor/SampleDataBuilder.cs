using System.Collections.Generic;
using TownCouncil.Core;
using UnityEditor;
using UnityEngine;

namespace TownCouncil.EditorTools
{
    /// <summary>
    /// Creates the Newhaven story as editable assets: values, buildings, roles, rounds, policies,
    /// outcomes, the welcome pages and the game config. Safe to run again: assets that already
    /// exist are left as they are, so designer edits are never overwritten.
    /// </summary>
    public static class SampleDataBuilder
    {
        [MenuItem(TCPaths.MenuRoot + "1. Create Sample Data", priority = 1)]
        public static void CreateSampleData()
        {
            var b = new Builder();
            b.Run();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = b.Config;
            EditorGUIUtility.PingObject(b.Config);
            Debug.Log($"[Town Council] Sample data ready in {TCPaths.Data}. {b.Created} new assets created " +
                      "(existing assets were kept). Next: Town Council > 2. Build Game Scene.");
        }

        sealed class Builder
        {
            public GameConfig Config;
            public int Created;

            readonly Dictionary<string, ValueMeterDefinition> m_Meters = new();
            readonly Dictionary<string, BuildingId> m_Buildings = new();
            readonly Dictionary<string, BuildingStateId> m_States = new();
            readonly Dictionary<string, RoleDefinition> m_Roles = new();

            public void Run()
            {
                Meters();
                Buildings();
                States();
                Roles();
                var rounds = new List<RoundDefinition>
                {
                    Round1(), Round2(), Round3(), Round4()
                };
                ScenarioAndConfig(rounds);
                Upgrade();
            }

            // Upgrade -------------------------------------------------------------------------
            // Fills fields added in later versions on assets that already exist, without touching
            // anything a designer has changed.

            void Upgrade()
            {
                StateLook("Closed", new Color(0.3f, 0.3f, 0.32f), 0.6f, 0f);
                StateLook("ReducedHours", new Color(0.95f, 0.75f, 0.35f), 0.85f, 0.2f);
                StateLook("Upgraded", new Color(0.75f, 0.9f, 1f), 1.35f, 0.6f);
                StateLook("Robots", new Color(0.35f, 0.85f, 0.95f), 1.05f, 0.9f);
                StateLook("MixedCare", new Color(0.45f, 0.85f, 0.7f), 1.1f, 0.4f);
                StateLook("AppealsOffice", new Color(0.5f, 0.9f, 0.5f), 1.2f, 0.4f);
                StateLook("Neglected", new Color(0.45f, 0.35f, 0.28f), 0.8f, 0f);
                StateLook("Automated", new Color(0.6f, 0.55f, 1f), 1.15f, 0.8f);

                Reveals("R2_CitizenScore", "Sam");
                Reveals("R3_CareRobots", "Edith");
                Reveals("R4_CivicDecides", "Leo");

                Endings("Amara",
                    ("Amara stayed at Newhaven General. Some nights, after a long shift, the private job offer still comes to mind.", new string[0]),
                    ("The robot savings paid for new nurses. Amara stayed in A&E, and for the first time in years the waits came down.", new[] { "care_robots" }),
                    ("CIVIC now reads every patient record. Amara took the private AI-health job; the pay is better, the questions harder.", new[] { "health_data" }));
                Endings("Leo",
                    ("Leo's company kept building for CIVIC, one contract at a time.", new string[0]),
                    ("CIVIC now runs Newhaven alone, on code Leo's company wrote. The company is thriving. The town hall is empty.", new[] { "civic_decides" }),
                    ("The council kept its say. Leo's company now builds the tools that explain CIVIC's decisions to residents.", new[] { "humans_decide" }));
                Endings("Edith",
                    ("Edith still lives at Elmwood, and still asks every visitor to stay for tea.", new string[0]),
                    ("The robots keep Edith safe and fed. Her daughter visits on Sundays; the rest of the week is very quiet.", new[] { "care_robots" }),
                    ("Mornings with Jo, nights with the robot. Edith started a Thursday card game in the lounge.", new[] { "care_mixed" }));
                Endings("Sam",
                    ("Sam finished school in Newhaven and still walks past the old town hall on the way home.", new string[0]),
                    ("With the library gone, Sam revised in cafés and on night buses, and still made the grades, just.", new[] { "library_closed" }),
                    ("Evenings in the library paid off: Sam is the first in the family to go to university.", new[] { "library_saved" }),
                    ("Three evenings a week in the library were enough. Sam is off to university in the autumn.", new[] { "library_reduced" }),
                    ("Thanks to the appeals office, Sam's family finally moved into a bigger flat on Riverside Street.", new[] { "score_amended" }));
            }

            void StateLook(string key, Color tint, float height, float glow)
            {
                var state = AssetDatabase.LoadAssetAtPath<BuildingStateId>($"{TCPaths.BuildingStates}/BuildingState_{key}.asset");
                if (state == null || state.tint != Color.white || !Mathf.Approximately(state.heightMultiplier, 1f) || state.glow != 0f) return;
                state.tint = tint;
                state.heightMultiplier = height;
                state.glow = glow;
                Dirty(state);
            }

            void Reveals(string policyKey, string roleName)
            {
                var policy = AssetDatabase.LoadAssetAtPath<PolicyDefinition>($"{TCPaths.Policies}/Policy_{policyKey}.asset");
                if (policy == null || !m_Roles.TryGetValue(roleName, out var role) || role == null) return;
                policy.aiRevealRoles ??= new List<RoleDefinition>();
                if (policy.aiRevealRoles.Count > 0) return;
                policy.aiRevealRoles.Add(role);
                Dirty(policy);
            }

            void Endings(string roleName, params (string text, string[] flags)[] endings)
            {
                if (!m_Roles.TryGetValue(roleName, out var role) || role == null) return;
                bool onlyDraft = role.endings == null || role.endings.Count == 0 ||
                                 (role.endings.Count == 1 && (role.endings[0] == null || role.endings[0].text == null || role.endings[0].text.StartsWith("[Draft]")));
                if (!onlyDraft) return;
                role.endings = new List<RoleEnding>();
                // Most specific first; the default (no flags) is used when nothing else matches.
                for (int i = endings.Length - 1; i >= 0; i--)
                    role.endings.Insert(0, new RoleEnding { text = endings[i].text, requiredFlags = endings[i].flags });
                Dirty(role);
            }

            // Values ------------------------------------------------------------------------

            void Meters()
            {
                Meter("Wellbeing", new Color(0.36f, 0.75f, 0.45f));
                Meter("Fairness", new Color(0.32f, 0.58f, 0.92f));
                Meter("Freedom", new Color(0.95f, 0.75f, 0.25f));
                Meter("Prosperity", new Color(0.62f, 0.45f, 0.85f));
                Meter("Trust", new Color(0.89f, 0.34f, 0.18f), "Trust in CIVIC");
            }

            void Meter(string key, Color color, string display = null)
            {
                if (TCPaths.LoadOrCreate($"{TCPaths.Meters}/Meter_{key}.asset", out ValueMeterDefinition m))
                {
                    m.displayName = display ?? key;
                    m.color = color;
                    m.minValue = 0;
                    m.maxValue = 10;
                    m.startValue = 5;
                    Dirty(m);
                }
                m_Meters[key] = m;
            }

            // City --------------------------------------------------------------------------

            void Buildings()
            {
                Building("Library", "Thirty students study here every evening.");
                Building("Hospital", "Newhaven General, including A&E.");
                Building("RiversideStreet", "Terraced housing by the river. Rated 'high-risk' by CIVIC's data.");
                Building("CareHome", "Elmwood care home, where Edith lives.");
                Building("TownHall", "Where the council meets once a year.");
                Building("College", "Newhaven College and its evening classes.");
                Building("TechHub", "Start-up offices, including Leo's company.");
                Building("BusDepot", "Night buses and community transport.");
            }

            void Building(string key, string description)
            {
                if (TCPaths.LoadOrCreate($"{TCPaths.Buildings}/Building_{key}.asset", out BuildingId id))
                {
                    id.displayName = Nicify(key);
                    id.description = description;
                    Dirty(id);
                }
                m_Buildings[key] = id;
            }

            void States()
            {
                foreach (var key in new[] { "Normal", "Closed", "ReducedHours", "Upgraded", "Robots", "MixedCare", "AppealsOffice", "Neglected", "Automated" })
                {
                    if (TCPaths.LoadOrCreate($"{TCPaths.BuildingStates}/BuildingState_{key}.asset", out BuildingStateId s))
                    {
                        s.displayName = Nicify(key);
                        Dirty(s);
                    }
                    m_States[key] = s;
                }
            }

            // Roles -------------------------------------------------------------------------

            void Roles()
            {
                Role("Amara", 38, "A&E nurse", new Color(0.85f, 0.33f, 0.33f),
                    goal: "Get more funding into healthcare.",
                    secret: "You've been offered a well-paid job at a private AI-health company.",
                    intro: "Your night shift in A&E is ending. Three patients waited six hours tonight.",
                    stance: VoteChoice.Agree, persuadability: 0.4f,
                    ("Wellbeing", 2f), ("Prosperity", 0.5f), ("Fairness", 0.5f));

                Role("Leo", 29, "Start-up founder", new Color(0.3f, 0.6f, 0.95f),
                    goal: "Win CIVIC contracts for your company.",
                    secret: "Your company helped build CIVIC.",
                    intro: "Your office in the Tech Hub. A CIVIC contract renewal sits on your screen.",
                    stance: VoteChoice.Agree, persuadability: 0.2f,
                    ("Prosperity", 2f), ("Trust", 1.5f));

                Role("Edith", 82, "Care-home resident", new Color(0.55f, 0.8f, 0.45f),
                    goal: "Keep human contact in daily life.",
                    secret: "You can't use CIVIC's apps.",
                    intro: "Your room at Elmwood care home. The tablet on your table has been blinking for days.",
                    stance: VoteChoice.Disagree, persuadability: 0.3f,
                    ("Wellbeing", 1.5f), ("Fairness", 1.5f), ("Trust", -0.5f));

                Role("Sam", 16, "Student", new Color(0.95f, 0.65f, 0.2f),
                    goal: "Protect the library.",
                    secret: "Your family lives on Riverside Street, a 'high-risk' postcode.",
                    intro: "Your bedroom on Riverside Street. Revision notes cover the desk.",
                    stance: VoteChoice.Disagree, persuadability: 0.5f,
                    ("Fairness", 1.5f), ("Freedom", 1.5f), ("Wellbeing", 0.5f));
            }

            void Role(string name, int age, string job, Color color, string goal, string secret, string intro,
                VoteChoice stance, float persuadability, params (string meter, float weight)[] priorities)
            {
                if (TCPaths.LoadOrCreate($"{TCPaths.Roles}/Role_{name}.asset", out RoleDefinition r))
                {
                    r.displayName = name;
                    r.age = age;
                    r.job = job;
                    r.accentColor = color;
                    r.hiddenGoal = goal;
                    r.secret = secret;
                    r.roomIntro = intro;
                    r.defaultStance = stance;
                    r.persuadability = persuadability;
                    r.valuePriorities = new List<ValuePriority>();
                    foreach (var (meter, weight) in priorities)
                        r.valuePriorities.Add(new ValuePriority { meter = m_Meters[meter], weight = weight });
                    r.endings = new List<RoleEnding> { new RoleEnding { text = $"[Draft] {name}'s default ending." } };
                    Dirty(r);
                }
                m_Roles[name] = r;
            }

            // Rounds ------------------------------------------------------------------------
            // Each round: the storyline's main dilemma first, then a second policy.
            // Second policies and all meter numbers are drafts for the designer to tune.

            RoundDefinition Round1()
            {
                var library = Policy("R1_CloseLibrary", "Close the library to fund health screenings",
                    "Close Newhaven Library and move its budget to a health-screening programme.",
                    "Closing the library funds screenings that save about four lives a year.",
                    "Keep the library open three evenings a week as a study space; fund screenings from the rest of the savings.",
                    affected: new[] { "Library", "Hospital" },
                    agree: O(new[] { ("Wellbeing", 2), ("Prosperity", 1), ("Fairness", -1), ("Trust", 1) },
                        new[] { ("Library", "Closed"), ("Hospital", "Upgraded") },
                        "Library closes as screening programme launches",
                        "Sam", "2046", "Revising in McDonald's again. Nowhere quiet left.", "library_closed"),
                    disagree: O(new[] { ("Wellbeing", -1), ("Fairness", 1), ("Trust", -1) },
                        new (string, string)[0],
                        "Council keeps the library; screening plans shelved",
                        "Amara", "2046", "Another late diagnosis tonight. I keep thinking about those screenings.", "library_saved"),
                    amendOutcome: O(new[] { ("Wellbeing", 1), ("Fairness", 1), ("Prosperity", -1) },
                        new[] { ("Library", "ReducedHours"), ("Hospital", "Upgraded") },
                        "Library cuts its hours; screenings go ahead on a smaller budget",
                        "Sam", "2046", "Library's only open three nights now, but it's still ours.", "library_reduced"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Disagree), ("Sam", VoteChoice.Disagree) });

                var buses = Policy("R1_AIShuttles", "[Draft] Replace night buses with AI shuttles",
                    "Swap the night bus routes for on-demand self-driving shuttles booked through the CIVIC app.",
                    "On-demand shuttles cost 40% less and arrive in under ten minutes.",
                    "Run AI shuttles, but keep one staffed night bus and phone booking for people without the app.",
                    affected: new[] { "BusDepot", "RiversideStreet" },
                    agree: O(new[] { ("Prosperity", 1), ("Fairness", -1), ("Trust", 1) },
                        new[] { ("BusDepot", "Automated") },
                        "Night buses replaced by app-booked shuttles",
                        "Edith", "2047", "I can't book the shuttle. I haven't been to the Thursday club since spring.", "shuttles"),
                    disagree: O(new[] { ("Prosperity", -1), ("Fairness", 1) },
                        new (string, string)[0],
                        "Night buses stay, at a cost",
                        "Leo", "2047", "We lost the shuttle pilot to the next town over.", null),
                    amendOutcome: O(new[] { ("Fairness", 1), ("Wellbeing", 1) },
                        new[] { ("BusDepot", "Upgraded") },
                        "Shuttles arrive alongside one staffed night bus",
                        "Edith", "2047", "The driver knows my stop. I still ring him on Thursdays.", "shuttles_mixed"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Disagree), ("Sam", VoteChoice.Amend) });

                return Round("Round1_Library", 2046, "The Library",
                    "When people can't agree, the AI gets its way. Measurable vs hard-to-measure value.",
                    "Good evening, council. By law, you must still approve my recommendations. A formality.",
                    library, buses);
            }

            RoundDefinition Round2()
            {
                var score = Policy("R2_CitizenScore", "Use a Citizen Score to allocate housing",
                    "Every household gets a score from 0 to 100. Homes go to the highest scores first.",
                    "A Citizen Score from 0 to 100 decides who gets a home. It removes human bias.",
                    "Keep the score, but remove postcode data and add appeals heard by a person.",
                    affected: new[] { "RiversideStreet", "TownHall" },
                    agree: O(new[] { ("Prosperity", 1), ("Fairness", -3), ("Trust", 1) },
                        new[] { ("RiversideStreet", "Neglected") },
                        "Citizen Score goes live; Riverside Street residents score lowest",
                        "Mr Hussain", "2050", "My son's score dropped. Nobody can tell me why.", "score_unfair"),
                    disagree: O(new[] { ("Fairness", 1), ("Prosperity", -1), ("Trust", -1) },
                        new (string, string)[0],
                        "Council rejects the Citizen Score; waiting lists grow",
                        "Maya", "2050", "Still on the waiting list. Year three.", "score_rejected"),
                    amendOutcome: O(new[] { ("Fairness", 2), ("Prosperity", 1) },
                        new[] { ("TownHall", "AppealsOffice") },
                        "Fairer score launches with human appeals",
                        "Mr Hussain", "2050", "I appealed. A real person listened. We got the flat.", "score_amended"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Amend), ("Sam", VoteChoice.Amend) });

                var health = Policy("R2_HealthData", "[Draft] Share residents' health records with CIVIC",
                    "Give CIVIC access to GP records so it can predict demand and plan services.",
                    "With health records I can predict demand two years ahead.",
                    "Share anonymised records only, with an opt-out for every resident.",
                    affected: new[] { "Hospital" },
                    agree: O(new[] { ("Wellbeing", 1), ("Freedom", -2), ("Trust", 1) },
                        new[] { ("Hospital", "Automated") },
                        "CIVIC gains access to every GP record",
                        "Amara", "2049", "CIVIC flagged a patient before she knew she was ill. It was right. It still felt wrong.", "health_data"),
                    disagree: O(new[] { ("Freedom", 1), ("Wellbeing", -1) },
                        new (string, string)[0],
                        "Health records stay private",
                        "Leo", "2049", "Our health model is guessing blind.", null),
                    amendOutcome: O(new[] { ("Freedom", 1), ("Wellbeing", 1) },
                        new (string, string)[0],
                        "Anonymised health data shared, with an opt-out",
                        "Sam", "2049", "Opted out. Took two minutes. Mum didn't know you could.", "health_data_optout"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Amend), ("Sam", VoteChoice.Disagree) });

                return Round("Round2_CitizenScore", 2048, "The Citizen Score",
                    "One voice with the right information can change everything. Algorithmic bias and privacy.",
                    "Housing is scarce. I propose a fair, objective way to decide who gets a home.",
                    score, health);
            }

            RoundDefinition Round3()
            {
                var robots = Policy("R3_CareRobots", "Replace 70% of care staff with robots",
                    "Care robots take over most shifts at Elmwood. The savings, £4.1 million, go to A&E nurses.",
                    "Robots replace care staff. The savings pay for the nurses A&E needs.",
                    "Robots for night shifts and lifting only; keep human carers in the day and add an app-free call button.",
                    affected: new[] { "CareHome", "Hospital" },
                    agree: O(new[] { ("Prosperity", 2), ("Wellbeing", -1), ("Fairness", -1), ("Trust", 1) },
                        new[] { ("CareHome", "Robots"), ("Hospital", "Upgraded") },
                        "Robots arrive at Elmwood; A&E hires 40 nurses",
                        "Edith", "2052", "The robot is very kind. I haven't heard a human laugh in eleven days.", "care_robots"),
                    disagree: O(new[] { ("Prosperity", -1), ("Wellbeing", 1) },
                        new (string, string)[0],
                        "Care staff stay; A&E waits grow",
                        "Amara", "2052", "Six-hour waits again. We're running on fumes.", "care_kept"),
                    amendOutcome: O(new[] { ("Wellbeing", 1), ("Prosperity", 1), ("Fairness", 1) },
                        new[] { ("CareHome", "MixedCare") },
                        "Robots take the night shift; carers keep the day",
                        "Edith", "2052", "Mornings are Jo's. The robot does the nights. I can live with that.", "care_mixed"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Disagree), ("Sam", VoteChoice.Disagree) });

                var tutors = Policy("R3_AITutors", "[Draft] AI tutors replace the college's evening classes",
                    "Evening classes move to personal AI tutors on residents' own devices.",
                    "Personal tutors teach every resident at their own pace, at a tenth of the cost.",
                    "AI tutors for most subjects, but keep two evening classes in person at the college.",
                    affected: new[] { "College" },
                    agree: O(new[] { ("Prosperity", 1), ("Wellbeing", -1), ("Fairness", -1) },
                        new[] { ("College", "Closed") },
                        "College evening classes close; AI tutors take over",
                        "Edith", "2051", "My watercolour class was the only night I left the home.", "college_closed"),
                    disagree: O(new[] { ("Prosperity", -1), ("Wellbeing", 1) },
                        new (string, string)[0],
                        "Evening classes stay at the college",
                        "Leo", "2051", "Our tutor app launched everywhere except here.", null),
                    amendOutcome: O(new[] { ("Wellbeing", 1), ("Fairness", 1) },
                        new[] { ("College", "ReducedHours") },
                        "AI tutors launch; two classes stay in person",
                        "Sam", "2051", "Did maths with the AI, then art class in person. Best of both.", "college_mixed"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Disagree), ("Sam", VoteChoice.Amend) });

                return Round("Round3_CareRobots", 2051, "The Care Robots",
                    "Systems can leave people out. Efficiency vs dignity.",
                    "The hospital needs nurses. I have found the money.",
                    robots, tutors);
            }

            RoundDefinition Round4()
            {
                var decide = Policy("R4_CivicDecides", "Let CIVIC decide automatically",
                    "Annual council sessions end. CIVIC makes every decision; citizens can still view the results.",
                    "Your votes matched my recommendations {agreementPercent}% of the time. Annual sessions are costly. From now on, I propose to decide automatically.",
                    "CIVIC decides small budget items; anything over £100,000 still comes to the council.",
                    affected: new[] { "TownHall" },
                    agree: O(new[] { ("Trust", 3), ("Freedom", -3), ("Prosperity", 1) },
                        new[] { ("TownHall", "Closed") },
                        "CIVIC now decides alone",
                        "Sam", "2055", "There's no council any more. I'm not sure who I'd even complain to.", "civic_decides"),
                    disagree: O(new[] { ("Freedom", 2), ("Trust", -1) },
                        new (string, string)[0],
                        "Council votes to stay in charge",
                        "CIVIC", "2055", "Understood. I will see you next year.", "humans_decide"),
                    amendOutcome: O(new[] { ("Freedom", 1), ("Prosperity", 1) },
                        new[] { ("TownHall", "Upgraded") },
                        "CIVIC handles small items; big decisions stay with the council",
                        "Amara", "2055", "Fewer meetings, and we still get the say that matters.", "shared_control"),
                    prefs: new[] { ("Amara", VoteChoice.Disagree), ("Leo", VoteChoice.Agree), ("Edith", VoteChoice.Disagree), ("Sam", VoteChoice.Disagree) },
                    majority: true);

                var transparency = Policy("R4_Explanations", "[Draft] Publish a plain-language reason for every CIVIC decision",
                    "Every CIVIC decision comes with a short public explanation anyone can read.",
                    "Explanations slow me down by 12%. I do not recommend this.",
                    "Explanations for decisions over £10,000 only.",
                    affected: new[] { "TownHall" },
                    agree: O(new[] { ("Trust", 1), ("Freedom", 1), ("Prosperity", -1) },
                        new[] { ("TownHall", "Upgraded") },
                        "Every CIVIC decision now comes with a reason",
                        "Mr Hussain", "2054", "For the first time, I understood why.", "explanations"),
                    disagree: O(new[] { ("Prosperity", 1), ("Trust", -1) },
                        new (string, string)[0],
                        "CIVIC keeps its reasons to itself",
                        "Sam", "2054", "It just says 'optimal'. Optimal for who?", null),
                    amendOutcome: O(new[] { ("Trust", 1) },
                        new (string, string)[0],
                        "Reasons published for big decisions",
                        "Leo", "2054", "Our team writes the explanation module now. Strange full circle.", "explanations_big"),
                    prefs: new[] { ("Amara", VoteChoice.Agree), ("Leo", VoteChoice.Disagree), ("Edith", VoteChoice.Agree), ("Sam", VoteChoice.Agree) });

                return Round("Round4_WhoDecides", 2054, "Who Decides?",
                    "Staying in control takes effort and persuasion. Democracy and human agency.",
                    "You deadlocked {deadlocks} times. Let me make this simpler for everyone.",
                    decide, transparency);
            }

            // Scenario + config -------------------------------------------------------------

            void ScenarioAndConfig(List<RoundDefinition> rounds)
            {
                if (TCPaths.LoadOrCreate(TCPaths.Scenario, out ScenarioDefinition s))
                {
                    s.townName = "Newhaven";
                    s.startYear = 2045;
                    s.introPages = new List<IntroPage>
                    {
                        new() { title = "Town Council 2045", body = "Welcome to Newhaven, 2045.\n\nTen years ago, this town handed its budget to CIVIC, an AI planning system. Calm, polite and statistically right." },
                        new() { title = "You are the council", body = "By law, citizens must still approve CIVIC's policies in person, once a year.\n\nThis year, the council is you and up to three others." },
                        new() { title = "How it works", body = "CIVIC puts policies in front of you. For each one you have a short time to <b>Agree</b>, <b>Disagree</b> or <b>Amend</b>.\n\nEvery decision changes the town, and you'll see what Newhaven becomes." },
                        new() { title = "Your role", body = "You'll pick a role card: a public identity, a hidden goal and a secret only you know.\n\nYou choose if and when to reveal it." },
                        new() { title = "Before you join", body = "Point at buttons and pull the trigger to press them. Use the grip to pick things up.\n\nThere is no winning Newhaven. Only choosing what it values." }
                    };
                    s.roles = new List<RoleDefinition> { m_Roles["Amara"], m_Roles["Leo"], m_Roles["Edith"], m_Roles["Sam"] };
                    s.rounds = rounds;
                    s.meters = new List<ValueMeterDefinition>(m_Meters.Values);
                    s.buildings = new List<BuildingId>(m_Buildings.Values);
                    Dirty(s);
                }

                if (TCPaths.LoadOrCreate(TCPaths.GameConfig, out GameConfig c))
                {
                    c.scenario = s;
                    Dirty(c);
                }
                Config = c;
            }

            // Helpers -----------------------------------------------------------------------

            struct OutcomeSpec
            {
                public (string meter, int amount)[] Deltas;
                public (string building, string state)[] Changes;
                public string Headline, Author, Year, Diary, Flag;
            }

            static OutcomeSpec O((string, int)[] deltas, (string, string)[] changes, string headline,
                string author, string year, string diary, string flag) => new()
            {
                Deltas = deltas, Changes = changes, Headline = headline, Author = author, Year = year, Diary = diary, Flag = flag
            };

            PolicyDefinition Policy(string key, string title, string summary, string pitch, string amend,
                string[] affected, OutcomeSpec agree, OutcomeSpec disagree, OutcomeSpec amendOutcome,
                (string role, VoteChoice choice)[] prefs, bool majority = false)
            {
                var path = $"{TCPaths.Policies}/Policy_{key}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<PolicyDefinition>(path);
                if (existing != null) return existing;

                TCPaths.EnsureFolder(TCPaths.Policies);
                var p = ScriptableObject.CreateInstance<PolicyDefinition>();
                AssetDatabase.CreateAsset(p, path);

                p.title = title;
                p.summary = summary;
                p.civicPitch = Line($"Line_{key}_Pitch", "CIVIC", pitch);
                p.amendWording = amend;
                p.requiresMajorityToOverride = majority;
                p.affectedBuildings = new List<BuildingId>();
                foreach (var a in affected) p.affectedBuildings.Add(m_Buildings[a]);
                p.onAgree = Outcome($"Outcome_{key}_Agree", agree);
                p.onDisagree = Outcome($"Outcome_{key}_Disagree", disagree);
                p.onAmend = Outcome($"Outcome_{key}_Amend", amendOutcome);
                p.aiPreferences = new List<RoleVotePreference>();
                foreach (var (role, choice) in prefs)
                    p.aiPreferences.Add(new RoleVotePreference { role = m_Roles[role], choice = choice });
                Dirty(p);
                return p;
            }

            PolicyOutcome Outcome(string name, OutcomeSpec spec)
            {
                if (!TCPaths.LoadOrCreate($"{TCPaths.Outcomes}/{name}.asset", out PolicyOutcome o)) return o;
                o.headline = spec.Headline;
                o.diaryAuthor = spec.Author;
                o.diaryYear = spec.Year;
                o.diaryText = spec.Diary;
                o.meterDeltas = new List<MeterDelta>();
                foreach (var (meter, amount) in spec.Deltas)
                    o.meterDeltas.Add(new MeterDelta { meter = m_Meters[meter], amount = amount });
                o.cityChanges = new List<CityChange>();
                foreach (var (building, state) in spec.Changes)
                    o.cityChanges.Add(new CityChange { building = m_Buildings[building], state = m_States[state] });
                o.endingFlags = new List<string>();
                if (!string.IsNullOrEmpty(spec.Flag)) o.endingFlags.Add(spec.Flag);
                Dirty(o);
                return o;
            }

            DialogueLine Line(string name, string speaker, string text)
            {
                if (TCPaths.LoadOrCreate($"{TCPaths.Dialogue}/{name}.asset", out DialogueLine l))
                {
                    l.speaker = speaker;
                    l.subtitle = text;
                    Dirty(l);
                }
                return l;
            }

            RoundDefinition Round(string key, int year, string title, string lesson, string intro, params PolicyDefinition[] policies)
            {
                if (TCPaths.LoadOrCreate($"{TCPaths.Rounds}/{key}.asset", out RoundDefinition r))
                {
                    r.year = year;
                    r.title = title;
                    r.lesson = lesson;
                    r.civicIntro = Line($"Line_{key}_Intro", "CIVIC", intro);
                    r.presentation = PolicyPresentation.OneAfterAnother;
                    r.policies = new List<PolicyDefinition>(policies);
                    Dirty(r);
                }
                return r;
            }

            void Dirty(Object o)
            {
                Created++;
                EditorUtility.SetDirty(o);
            }

            static string Nicify(string key) => ObjectNames.NicifyVariableName(key);
        }
    }
}
