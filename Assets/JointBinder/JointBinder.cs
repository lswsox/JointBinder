using System.Collections.Generic;
using UnityEngine;

// JointBinder requires that the direction of the bone is -Y. (본의 방향이 유니티 -Y 방향이라는 전제로 제작)
public class JointBinder : MonoBehaviour
{
    public bool m_bindTwist = false; // 일단 Y축 트위스트로만 작동 (유니티 피직스는 Y축으로 이어지는 조인트에 좋은 품질로 작동함.)
    [Range(0f, 1f)]
    public float m_bindTwistStrength = 0.15f;
    public float m_bindDistanceLimit = 0.04f;
    public List<Rigidbody> m_rigidbodies = new List<Rigidbody>();
    private List<Vector3> m_localVectors = new List<Vector3>(); // 부모 자식간 상대 위치 기록

    void Start()
    {
        // 리지드바디 배열에 빈 공간이 있으면 삭제
        for (int i = 0; i < m_rigidbodies.Count;)
        {
            if (m_rigidbodies[i] == null)
            {
                m_rigidbodies.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }

        if (m_rigidbodies.Count > 1)
        {
            m_localVectors.Add(Vector3.zero); // for 문에서 연산량을 최소로 하기 위해 Vector3 하나 더 사용해서 숫자를 맞춘다.
            for (int i = 1; i < m_rigidbodies.Count; i++)
            {
                // 여기서 부모 기준 자식의 위치들을 m_localVectors 에다가 기록.
                Vector3 localPos = m_rigidbodies[i - 1].transform.InverseTransformPoint(m_rigidbodies[i].transform.position);
                m_localVectors.Add(localPos);
            }
        }
    }

    void FixedUpdate()
    {
        if (m_rigidbodies.Count > 1)
        {
            BindDistance();

            if (m_bindTwist)
            {
                BindTwist();
            }
        }
    }

    // FixedUpdate 에서 m_rigidbodies.Count > 1 검사를 했다는 전제로 작동함
    private void BindDistance()
    {
        for (int i = 1; i < m_rigidbodies.Count; i++)
        {
            // 월드 포지션 기준
            Vector3 initWorldPos = m_rigidbodies[i - 1].transform.TransformPoint(m_localVectors[i]);
            Vector3 physicsOffset = m_rigidbodies[i].position - initWorldPos;
            if (physicsOffset.magnitude > m_bindDistanceLimit)
            {
                physicsOffset = physicsOffset.normalized * m_bindDistanceLimit;
                m_rigidbodies[i].transform.position = initWorldPos + physicsOffset; // API문서에는 Rigidbody.position 을 변경하는게 빠르다고 나와있지만, 순간적으로 찢어지는 증세가 발생하여 Rigidbody.transform.position을 변경함.
            }
        }
    }

    // FixedUpdate 에서 m_rigidbodies.Count > 1 검사를 했다는 전제로 작동함
    private void BindTwist()
    {
        // 로컬 Transform 연산을 최소화 하기 위해 모든 포지션은 World 기준으로.
        // 모든 본의 월드 포지션 좌표를 미리 구해둔다. 부모 자식간으로 연결된 경우도 있을 수 있으므로 트위스트에 개입한 위 틀어지기 전의 위치를 미리 알아두기 위한 목적
        Vector3[] wPositions = new Vector3[m_rigidbodies.Count];
        Vector3[] wLookPositions = new Vector3[m_rigidbodies.Count];
        wPositions[0] = m_rigidbodies[0].position;
        wLookPositions[0] = Vector3.zero; // 0은 쓰지 않지만 채워둔다.

        // 리지드바디가 부모자식간으로 연결된 경우도 있을 수 있어서 트위스트에 개입하기 전에 wPositions와 wLookPositions를 미리 조사한다.
        for (int i = 1; i < m_rigidbodies.Count; i++)
        {
            wPositions[i] = m_rigidbodies[i].position;
            float dist = Vector3.Distance(wPositions[i], wPositions[i - 1]);
            wLookPositions[i] = m_rigidbodies[i].transform.TransformPoint(new Vector3(0f, -dist, 0f));
        }

        // 미리 조사한 wPositions와 wLookPositions를 기반으로 트위스트에 개입
        for (int i = 1; i < m_rigidbodies.Count; i++)
        {
            Vector3 lookVector = (wLookPositions[i] - wPositions[i - 1]).normalized;
            Vector3 up = m_rigidbodies[i - 1].transform.right;
            Vector3 crossVector = Vector3.Cross(lookVector, up); // Twist가 전혀 없는 LookAt용 Up 벡터 (크로스 연산 결과 만들어진 벡터)
            Vector3 upOriginal = m_rigidbodies[i].transform.forward; // Twist가 발생하는 원래 피직스의 LookAt용 Up 벡터
            Vector3 upLerp = Vector3.Lerp(upOriginal, crossVector, m_bindTwistStrength); // Twist가 너무 없어도 자연스럽지 않으니 Lerp로 보간해서 사용함. (천천히 Twist가 풀리도록)

            // transform.LookAt을 쿼터니언 Quaternion.LookRotation으로 테스트 해봤으나 피직스 안정성이 떨어져서 transform.LookAt을 사용함.
            m_rigidbodies[i].transform.LookAt(wLookPositions[i], upLerp); // cross 벡터 위치를 바라본다.
            m_rigidbodies[i].transform.Rotate(new Vector3(-90f, 0f, 0f)); // 축 방향 보정
        }
    }
}
