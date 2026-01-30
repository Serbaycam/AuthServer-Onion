namespace AuthServer.Identity.Domain.Constants
{
    public static class Permissions
    {
        // Modül 1: Laboratuvar
        public static class Laboratories
        {
            public const string View = "Permissions.Laboratories.View";
            public const string Create = "Permissions.Laboratories.Create";
            public const string Edit = "Permissions.Laboratories.Edit";
            public const string Delete = "Permissions.Laboratories.Delete";
        }

        // Helper Metod: Tüm izinleri listeler (Seeding sırasında lazım olabilir)
        public static List<string> GeneratePermissionsForModule(string module)
        {
            return new List<string>
            {
                $"Permissions.{module}.Create",
                $"Permissions.{module}.View",
                $"Permissions.{module}.Edit",
                $"Permissions.{module}.Delete",
            };
        }
    }
}