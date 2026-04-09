# Result: sub-agents

- prompts:

  ```txt
  please implement the product as specified in `/home/dev/src/fancy_zones/PRODUCT_REQUIREMENTS.md`.

  I want you to act as an engineering manager. you do not perform any implementation work yourself, but instead of coordinate a fleet of sub-agents to perform the various phases of the implementation. you spawn the sub-agents
  sequentially and have each one work until it is done. then you spawn the next one, until the product is successfully implemented.

  - first, you spawn a sub-agent to create the architecture, i.e. project structure, modules, etc., including testing infrastructure that accounts for the fact that the real app will run on Windows, but development and unit
  testing are happening on Linux (e.g. creating a mocking layer for the windows API integrations)
  - then, you spawn a planner sub-agent that splits the implementation into multiple smaller chunks
  - then you spawn a sub-agent to implement each chunk at a time, using red-green TDD, to ensure a properly tested result
  - once the implementation is considered complete by the sub-agents, you spawn a validating sub-agent that independently critically reviews and validates the result
  - finally, any feedback from the validating sub-agent is fed into one more sub-agent to implement any review findings

  you only accept the result once you are happy with the quality and are willing to take accountability for any issues that are found in production
  ```

- it created an app that crashed on launch
