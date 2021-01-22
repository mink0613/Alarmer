using System;

namespace Alarmer
{
    public enum AlarmType
    {
        Weekly,
        Monthly
    }

    public class AlarmData
    {
        public string Name;

        public int Date;

        public DayOfWeek Day;

        public AlarmType Type;
    }
}
