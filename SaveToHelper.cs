using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackupperKISS
{
  public class SaveToHelper
  {
    private List<(string param, string comment)> content = new List<(string, string)>();

    public SaveToHelper()
    {

    }

    public void Add(string param, string comment)
    {
      if (param == null)
      {
        param = String.Empty;
      }

      if (comment == null)
      {
        comment = String.Empty;
      }

      content.Add((param, comment));
    }

    public void AddToList(List<string> list)
    {
      var maxlen = content.Select(c => c.param.Length).Max() + 2;

      foreach (var item in content)
      {
        list.Add(item.param.PadRight(maxlen) + item.comment);
      }
    }
  }
}
