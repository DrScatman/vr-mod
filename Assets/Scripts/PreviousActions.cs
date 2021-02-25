using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PreviousActions
{
    private static Stack<PreviousData> prevActions = new Stack<PreviousData>();

    public class PreviousData
    {
        public GameObject obj;
        public ActionType actionType;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public enum ActionType
    {
        Spawn,
        Transform,
        None
    }

    public static void AddPreviousAction(ActionType actionType, GameObject obj,
        Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Vector3 scale = new Vector3())
    {
        AddPreviousAction(new PreviousData
        {
            obj = obj,
            actionType = actionType,
            position = position,
            rotation = rotation,
            scale = scale
        });
    }

    public static void AddPreviousAction(PreviousData action)
    {
        prevActions.Push(action);
    }

    public static void Undo()
    {
        if (prevActions.Count > 0)
        {
            PreviousData data = prevActions.Pop();
            if (data.obj == null) return;

            switch (data.actionType)
            {
                case ActionType.Spawn:
                    data.obj.SetActive(false);
                    break;
                case ActionType.Transform:
                    Transform objT = data.obj.transform;
                    objT.position = data.position;
                    objT.rotation = data.rotation;
                    objT.localScale = data.scale;
                    break;
            }
        }
    }

    public static PreviousData Peek()
    {
        if (prevActions.Count == 0)
            return new PreviousData { actionType = ActionType.None };

        return prevActions.Peek();
    }

    public static int Count()
    {
        return prevActions.Count;
    }
}
