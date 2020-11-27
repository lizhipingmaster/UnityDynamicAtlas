using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class Util
{
    public static void ForEach<T>(this T[] array, Action<T> action)
    {
        //foreach (var e in array)
        for(int i = 0; i < array.Length; ++i)
        {
            action(array[i]);
        }
    }

    public static T Find<T>(this T[] array, Predicate<T> predicate)
    {
        for (int i = 0; i < array.Length; ++i)
        {
            if (predicate(array[i]))
                return array[i];
        }

        return default(T);
    }

    public static void DelayExecute(this UnityEngine.MonoBehaviour obj, float seconds, Action action)
    {
        obj.StartCoroutine(Delay(seconds, action));
    }

    static System.Collections.IEnumerator Delay(float seconds, Action action)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);
        action.Invoke();
    }
}
