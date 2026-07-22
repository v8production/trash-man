public class Define
{
    public enum InputMode
    {
        Player,
        UI,
        Cinematic,
    }

    public enum MonsterAnimState
    {
        IDLE,
        RUN,
        ATTACK,
        HURT,
    }

    public enum Scene
    {
        Unknown,
        Intro,
        Lobby,
        Fusion,
        Game,

    }

    public enum GameEndResult
    {
        None,
        Victory,
        GameOver,
    }

    public enum Layer
    {
        Monster = 8,
        Ground = 9,
        Block = 10,

    }

    public enum Sound
    {
        Bgm,
        Effect,
        MaxCount,
    }

    public enum UIEvent
    {
        Click,
        Drag,
    }

    public enum MouseEvent
    {
        Move,
        Press,
        PointerDown,
        PointerUp,
        Click,
    }
    public enum CameraMode
    {
        QuarterView,
    }

    public static float epsilon = 1e-8f;

    public enum TitanRole
    {
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        LeftLeg = 4,
        RightLeg = 5
    }

    public struct TitanRoleColorSet
    {
        public int RangerBodyRgb;
        public int RangerFaceRgb;
        public float RangerFaceEmissive;
        public int BoardPenRgb;
        public int NicknameTextRgb;
    }

    public static readonly TitanRoleColorSet DefaultTitanRoleColors = new TitanRoleColorSet
    {
        RangerBodyRgb = 0x808080,
        RangerFaceRgb = 0xFFFFFF,
        RangerFaceEmissive = 1f,
        BoardPenRgb = 0xFFFFFF,
        NicknameTextRgb = 0x808080,
    };

    public static readonly TitanRole[] TitanRoleColorPriority =
    {
        TitanRole.Torso,
        TitanRole.RightLeg,
        TitanRole.LeftLeg,
        TitanRole.RightArm,
        TitanRole.LeftArm,
    };

    public static readonly TitanRoleColorSet[] TitanRoleColorTable =
    {
        // Torso / Red
        new TitanRoleColorSet { RangerBodyRgb = 0xAC0000, RangerFaceRgb = 0xAC0000, RangerFaceEmissive = 10f, BoardPenRgb = 0xE00000, NicknameTextRgb = 0xAC0000 },
        // LeftArm / Black
        new TitanRoleColorSet { RangerBodyRgb = 0x0F0F0F, RangerFaceRgb = 0xE9E9E9, RangerFaceEmissive = 3f, BoardPenRgb = 0x0F0F0F, NicknameTextRgb = 0x0F0F0F },
        // RightArm / Yellow
        new TitanRoleColorSet { RangerBodyRgb = 0xF7C600, RangerFaceRgb = 0xF7C600, RangerFaceEmissive = 3f, BoardPenRgb = 0xFFCC00, NicknameTextRgb = 0xF7C600 },
        // LeftLeg / Green
        new TitanRoleColorSet { RangerBodyRgb = 0x42AA00, RangerFaceRgb = 0x42AA00, RangerFaceEmissive = 10f, BoardPenRgb = 0x31BE00, NicknameTextRgb = 0x42AA00 },
        // RightLeg / Blue
        new TitanRoleColorSet { RangerBodyRgb = 0x1E37B5, RangerFaceRgb = 0x1E37B5, RangerFaceEmissive = 10f, BoardPenRgb = 0x002AFF, NicknameTextRgb = 0x1E37B5 },
    };

    public enum GrolarAnimState
    {
        Run00,
        Walk00,
        Alert00_Roar,
        Hit00,
        Attack00_Alert,
        Attack00_Swing,
        Attack00_Rebound
    }

    public enum RangerAnimState
    {
        Idle00,
        Walk00,
        Walk01,
        Emote00,
        Emote01,
        Emote02,
        Sit00,
        Sit01,
        Sit02,
    }
}
