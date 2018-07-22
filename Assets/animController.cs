using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class animController : MonoBehaviour {

    Animator anim;



    public bool punchAnim;

    public bool danceAnim;


    // Use this for initialization
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (punchAnim)
        {
            anim.SetBool("punch", true);
        }
        else if (danceAnim)
        {
            anim.SetBool("dance", true);
        }
        else
        {
            anim.SetBool("dance", false);
            anim.SetBool("punch", false);
        }

        

        punchAnim = false;
        danceAnim = false;
    }

    public void clickDance()
    {
        resetBools();

        danceAnim = true;
    }

    public void clickFight()
    {
        resetBools();

        punchAnim = true;
        
    }

    void resetBools()
    {
        danceAnim = false;
        punchAnim = false;
    }
}
