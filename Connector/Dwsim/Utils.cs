namespace Connector;

public class Utils
{
    // Some of the properties in DWSIM do not have a common interface containing the "Values"/"Value" property.
    // So we need to wrap them in a class to be able to read the values later on in the same way.
    class WrappedListProperty
    {
        public object Values { get; set; }
        public string Name { get; set; }
        public bool CanModify { get; set; }

        public WrappedListProperty(dynamic values, string name)
        {
            Values = values;
            Name = name;
            CanModify = false;
        }
    }
}